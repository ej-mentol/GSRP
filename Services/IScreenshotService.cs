using GSRP.Models;
using System.Threading.Tasks;

namespace GSRP.Services
{
    public interface IScreenshotService
    {
        Task CreateAndCopyToClipboardAsync(Player player);
    }
}
