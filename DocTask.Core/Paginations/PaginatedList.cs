using DocTask.Core.Dtos.SubTasks;
using Microsoft.EntityFrameworkCore;

namespace DocTask.Core.Paginations
{
    public class PaginatedList<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public PaginatedMetaData MetaData { get; set; } = new PaginatedMetaData();

        public PaginatedList() { }

        // Constructor từ danh sách và MetaData
        public PaginatedList(List<T> items, PaginatedMetaData metaData)
        {
            Items.AddRange(items);
            MetaData = new PaginatedMetaData
            {
                PageIndex = metaData.PageIndex,
                TotalPages = metaData.TotalPages,
                TotalItems = metaData.TotalItems,
                CurrentItems = items.Count
            };
        }

        // Constructor từ danh sách, tổng số item, page index và page size
        public PaginatedList(List<T> items, int totalCount, int pageIndex, int pageSize)
        {
            Items.AddRange(items);
            MetaData.PageIndex = pageIndex;
            MetaData.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            MetaData.TotalItems = totalCount;
            MetaData.CurrentItems = items.Count;
        }
    }

    // Extension method để chuyển IQueryable<T> sang PaginatedList<T>
    public static class PaginatedListHelper
    {
        private const int DefaultPageSize = 10;
        private const int DefaultCurrentPage = 1;

        public static async Task<PaginatedList<T>> ToPaginatedListAsync<T>(
            this IQueryable<T> query,
            PageOptionsRequest options)
        {
            int page = options.Page > 0 ? options.Page : DefaultCurrentPage;
            int size = options.Size > 0 ? options.Size : DefaultPageSize;

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            return new PaginatedList<T>(items, totalCount, page, size);
        }
    }
}