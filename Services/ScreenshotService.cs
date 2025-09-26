using GSRP.Models;
using GSRP.View.Controls;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GSRP.Services
{
    public class ScreenshotService : IScreenshotService
    {
        public async Task CreateAndCopyToClipboardAsync(Player player)
        {
            if (player == null) return;

            // We need to create and render the control on the UI thread.
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var card = new ShareablePlayerCard
                {
                    DataContext = player
                };

                // Use the card's defined size for measurement and arrange
                var cardWidth = 450;
                var cardHeight = 180;
                card.Measure(new Size(cardWidth, cardHeight));
                card.Arrange(new Rect(new Size(cardWidth, cardHeight)));

                // Force the layout to update and bindings to be applied.
                card.UpdateLayout();

                // Render the control to a bitmap.
                var renderTargetBitmap = new RenderTargetBitmap(
                    cardWidth,
                    cardHeight,
                    96, // DPI X
                    96, // DPI Y
                    PixelFormats.Pbgra32);

                renderTargetBitmap.Render(card);
                renderTargetBitmap.Freeze(); // Freeze for performance and to allow cross-thread access if needed later

                // Copy the bitmap to the clipboard.
                Clipboard.SetImage(renderTargetBitmap);
            });
        }
    }
}
