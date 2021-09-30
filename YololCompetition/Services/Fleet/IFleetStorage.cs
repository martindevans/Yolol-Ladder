using System.Threading.Tasks;

namespace YololCompetition.Services.Fleet
{
    public interface IFleetStorage
    {
        Task<Fleet> Store(ulong userId, string name, byte[] bytes);

        Task<Fleet?> Load(ulong fleedId);

        Task<byte[]?> LoadBlob(ulong userId);

        Task<int> Delete(ulong id);
    }
}
