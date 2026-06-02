namespace AspNetCore.Api.Modules.Identity.Contracts
{
    public sealed class PagedResponse<T>
    {
        public IReadOnlyCollection<T> Items { get; set; } = Array.Empty<T>();

        public int PageIndex { get; set; }

        public int PageSize { get; set; }

        public int TotalCount { get; set; }

        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}
