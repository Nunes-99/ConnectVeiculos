namespace ConnectVeiculos.Application.ViewModels.Common
{
    /// <summary>
    /// Resultado paginado generico
    /// </summary>
    /// <typeparam name="T">Tipo dos itens</typeparam>
    public class PagedResult<T>
    {
        /// <summary>
        /// Lista de itens da pagina atual
        /// </summary>
        public List<T> Items { get; set; } = new();

        /// <summary>
        /// Total de registros no banco (sem paginacao)
        /// </summary>
        public int TotalItems { get; set; }

        /// <summary>
        /// Pagina atual (comeca em 1)
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Quantidade de itens por pagina
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total de paginas
        /// </summary>
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalItems / PageSize) : 0;

        /// <summary>
        /// Indica se existe pagina anterior
        /// </summary>
        public bool HasPreviousPage => Page > 1;

        /// <summary>
        /// Indica se existe proxima pagina
        /// </summary>
        public bool HasNextPage => Page < TotalPages;

        public PagedResult() { }

        public PagedResult(List<T> items, int totalItems, int page, int pageSize)
        {
            Items = items;
            TotalItems = totalItems;
            Page = page;
            PageSize = pageSize;
        }
    }
}
