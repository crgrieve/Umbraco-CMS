using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models;

namespace Umbraco.Core.Services
{
    public static class ContentTypeServiceExtensions
    {
        /// <summary>
        /// Returns the available composite content types for a given content type
        /// </summary>
        /// <param name="allContentTypes"></param>
        /// <param name="filterContentTypes">
        /// This is normally an empty list but if additional content type aliases are passed in, any content types containing those aliases will be filtered out
        /// along with any content types that have matching property types that are included in the filtered content types
        /// </param>
        /// <param name="ctService"></param>
        /// <param name="source"></param>
        /// <param name="filterPropertyTypes">
        /// This is normally an empty list but if additional property type aliases are passed in, any content types that have these aliases will be filtered out.
        /// This is required because in the case of creating/modifying a content type because new property types being added to it are not yet persisted so cannot
        /// be looked up via the db, they need to be passed in.
        /// </param>
        /// <returns></returns>
        public static IEnumerable<Tuple<IContentTypeComposition, bool>> GetAvailableCompositeContentTypes(this IContentTypeService ctService,
            IContentTypeComposition source,
            IContentTypeComposition[] allContentTypes,
            string[] filterContentTypes = null,
            string[] filterPropertyTypes = null)
        {            
            filterContentTypes = filterContentTypes == null
                ? new string[] { }
                : filterContentTypes.Where(x => x.IsNullOrWhiteSpace() == false).ToArray();

            filterPropertyTypes = filterPropertyTypes == null
                ? new string[] {}
                : filterPropertyTypes.Where(x => x.IsNullOrWhiteSpace() == false).ToArray();

            //create the full list of property types to use as the filter
            //this is the combination of all property type aliases found in the content types passed in for the filter
            //as well as the specific property types passed in for the filter
            filterPropertyTypes = allContentTypes
                    .Where(c => filterContentTypes.InvariantContains(c.Alias))
                    .SelectMany(c => c.PropertyTypes)
                    .Select(c => c.Alias)
                    .Union(filterPropertyTypes)
                    .ToArray();

            var sourceId = source != null ? source.Id : 0;
                        
            // find out if any content type uses this content type
            var isUsing = allContentTypes.Where(x => x.ContentTypeComposition.Any(y => y.Id == sourceId)).ToArray();
            if (isUsing.Length > 0)
            {
                //if already in use a composition, do not allow any composited types
                return new List<Tuple<IContentTypeComposition, bool>>();
            }

            // if it is not used then composition is possible
            // hashset guarantees unicity on Id
            var list = new HashSet<IContentTypeComposition>(new DelegateEqualityComparer<IContentTypeComposition>(
                (x, y) => x.Id == y.Id,
                x => x.Id));

            // usable types are those that are top-level
            var usableContentTypes = allContentTypes
                .Where(x => x.ContentTypeComposition.Any() == false).ToArray();
            foreach (var x in usableContentTypes)
                list.Add(x);

            // indirect types are those that we use, directly or indirectly
            var indirectContentTypes = GetDirectOrIndirect(source).ToArray();
            foreach (var x in indirectContentTypes)
                list.Add(x);

            //At this point we have a list of content types that 'could' be compositions
            
            //now we'll filter this list based on the filters requested
            var filtered = list
                //not itself
                .Where(x => x.Id != sourceId)
                .Where(x =>
                {
                    //need to filter any content types that are included in this list
                    return filterContentTypes.Any(c => c.InvariantEquals(x.Alias)) == false;
                })
                .Where(x =>
                {
                    //need to filter any content types that have matching property aliases that are included in this list                    
                    //ensure that we don't return if there's any overlapping property aliases from the filtered ones specified
                    return filterPropertyTypes.Intersect(
                        x.PropertyTypes.Select(p => p.Alias), 
                        StringComparer.InvariantCultureIgnoreCase).Any() == false;
                })
                .OrderBy(x => x.Name)                
                .ToList();

            //now we can create our result based on what is still available
            var result = list
                .OrderBy(x => x.Name)
                .Select(composition => filtered.Contains(composition)
                ? new Tuple<IContentTypeComposition, bool>(composition, true)
                : new Tuple<IContentTypeComposition, bool>(composition, false)).ToList();

            return result;
        }

        /// <summary>
        /// Get those that we use directly or indirectly
        /// </summary>
        /// <param name="ctype"></param>
        /// <returns></returns>
        private static IEnumerable<IContentTypeComposition> GetDirectOrIndirect(IContentTypeComposition ctype)
        {
            if (ctype == null) return Enumerable.Empty<IContentTypeComposition>();

            // hashset guarantees unicity on Id
            var all = new HashSet<IContentTypeComposition>(new DelegateEqualityComparer<IContentTypeComposition>(
                (x, y) => x.Id == y.Id,
                x => x.Id));

            var stack = new Stack<IContentTypeComposition>();

            if (ctype != null)
            {
                foreach (var x in ctype.ContentTypeComposition)
                    stack.Push(x);
            }

            while (stack.Count > 0)
            {
                var x = stack.Pop();
                all.Add(x);
                foreach (var y in x.ContentTypeComposition)
                    stack.Push(y);
            }

            return all;
        }

    }
}