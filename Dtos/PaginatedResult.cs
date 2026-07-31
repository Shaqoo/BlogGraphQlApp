namespace BlogGraphQlApp.Dtos
{
    public class PaginatedResult<T>
    {
        public IReadOnlyList<T> Items { get; private set; } = [];
        public int Page { get; private set; }
        public int PageSize { get; private set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
        public int TotalItems { get; private set; }
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;

        private PaginatedResult() { }

        public static PaginatedResult<T> Create(
            List<T> items,
            int page,
            int pageSize,
            int totalItems)
        {
            return new PaginatedResult<T>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };
        }
    }

}
