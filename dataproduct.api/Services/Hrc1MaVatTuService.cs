using dataproduct.api.DTOs;
using dataproduct.api.Repositories;

namespace dataproduct.api.Services
{
    public class Hrc1MaVatTuService
    {
        private readonly IHrc1MaVatTuRepository _repo;

        public Hrc1MaVatTuService(IHrc1MaVatTuRepository repo)
        {
            _repo = repo;
        }

        public Task<(IEnumerable<Hrc1MaVatTuItem> Data, int TotalCount)> SearchAsync(Hrc1MaVatTuSearchRequest req)
            => _repo.SearchAsync(req);

        public Task<Hrc1MaVatTuItem?> GetByIdAsync(int id)
            => _repo.GetByIdAsync(id);

        public Task<Hrc1MaVatTuItem> CreateAsync(Hrc1MaVatTuUpsertDto dto)
            => _repo.CreateAsync(dto);

        public Task<Hrc1MaVatTuBulkCreateResult> BulkCreateAsync(List<Hrc1MaVatTuUpsertDto> items)
            => _repo.BulkCreateAsync(items);

        public Task<bool> UpdateAsync(int id, Hrc1MaVatTuUpsertDto dto)
            => _repo.UpdateAsync(id, dto);

        public Task<bool> DeleteAsync(int id)
            => _repo.DeleteAsync(id);
    }
}
