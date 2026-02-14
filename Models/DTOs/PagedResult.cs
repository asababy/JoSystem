using System.Collections.Generic;

namespace JoSystem.Models.DTOs
{
    public class PagedResult<T>
    {
        public int Total { get; set; }
        public List<T> Items { get; set; } = new List<T>();
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
    }
}
