using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using L1MapViewer.Helper;
using L1MapViewer.Models;

namespace L1MapViewer.Rendering
{
    /// <summary>
    /// 地圖渲染核心 - 整合 ViewportRenderer、OverlayRenderer、MiniMapRenderer
    /// 提供統一的渲染介面，不依賴 WinForms 控件
    /// </summary>
    public class MapRenderingCore
    {
        private readonly ViewportRenderer _viewportRenderer;
        private readonly OverlayRenderer _overlayRenderer;
        private readonly MiniMapRenderer _miniMapRenderer;

        /// <summary>
        /// 區塊寬度（像素）
        /// </summary>
        public const int BlockWidth = 3072;  // 64 * 24 * 2

        /// <summary>
        /// 區塊高度（像素）
        /// </summary>
        public const int BlockHeight = 1536; // 64 * 12 * 2

        /// <summary>
        /// 建立渲染核心實例
        /// </summary>
        public MapRenderingCore()
        {
            _viewportRenderer = new ViewportRenderer();
            _overlayRenderer = new OverlayRenderer();
            _miniMapRenderer = new MiniMapRenderer();
        }

        /// <summary>
        /// 渲染指定區域的地圖（同步版本）
        /// </summary>
        /// <param name="worldRect">要渲染的世界座標區域</param>
        /// <param name="s32Files">S32 檔案集合</param>
        /// <param name="checkedFiles">要顯示的 S32 檔案路徑集合</param>
        /// <param name="options">渲染選項</param>
        /// <returns>渲染後的 Bitmap</returns>
        public Bitmap RenderViewport(
            Rectangle worldRect,
            Dictionary<string, S32Data> s32Files,
            HashSet<string> checkedFiles,
            RenderOptions options)
        {
            // 1. 使用 ViewportRenderer 渲染基礎層
            var bitmap = _viewportRenderer.RenderViewport(
                worldRect,
                s32Files,
                checkedFiles,
                options.ShowLayer1,
                options.ShowLayer2,
                options.ShowLayer4,
                out _);

            // 2. 使用 OverlayRenderer 渲染覆蓋層
            if (options.HasOverlays)
            {
                var visibleS32Files = s32Files.Values
                    .Where(s => checkedFiles.Contains(s.FilePath))
                    .ToList();

                _overlayRenderer.RenderOverlays(bitmap, worldRect, visibleS32Files, options);
            }

            return bitmap;
        }

        /// <summary>
        /// 渲染指定區域的地圖（非同步版本）
        /// </summary>
        public Task<Bitmap> RenderViewportAsync(
            Rectangle worldRect,
            Dictionary<string, S32Data> s32Files,
            HashSet<string> checkedFiles,
            RenderOptions options,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return null;

                return RenderViewport(worldRect, s32Files, checkedFiles, options);
            }, cancellationToken);
        }

        /// <summary>
        /// 使用 MapDocument 渲染 Viewport
        /// </summary>
        public Bitmap RenderViewport(
            Rectangle worldRect,
            MapDocument document,
            RenderOptions options)
        {
            return RenderViewport(worldRect, document.S32Files, document.CheckedS32Files, options);
        }

        /// <summary>
        /// 使用 MapDocument 渲染 Viewport（非同步版本）
        /// </summary>
        public Task<Bitmap> RenderViewportAsync(
            Rectangle worldRect,
            MapDocument document,
            RenderOptions options,
            CancellationToken cancellationToken = default)
        {
            return RenderViewportAsync(worldRect, document.S32Files, document.CheckedS32Files, options, cancellationToken);
        }

        /// <summary>
        /// 渲染小地圖
        /// </summary>
        /// <param name="mapWidth">地圖寬度（像素）</param>
        /// <param name="mapHeight">地圖高度（像素）</param>
        /// <param name="targetSize">目標小地圖大小</param>
        /// <param name="s32Files">S32 檔案集合</param>
        /// <param name="checkedFiles">要顯示的 S32 檔案路徑集合</param>
        /// <param name="bounds">輸出：小地圖邊界資訊，用於座標轉換</param>
        /// <returns>渲染後的小地圖 Bitmap</returns>
        public Bitmap RenderMiniMap(
            int mapWidth,
            int mapHeight,
            int targetSize,
            Dictionary<string, S32Data> s32Files,
            HashSet<string> checkedFiles,
            out MiniMapRenderer.MiniMapBounds bounds)
        {
            return _miniMapRenderer.RenderMiniMap(
                mapWidth,
                mapHeight,
                targetSize,
                s32Files,
                checkedFiles,
                out _,
                out bounds);
        }

        /// <summary>
        /// 使用 MapDocument 渲染小地圖
        /// </summary>
        public Bitmap RenderMiniMap(MapDocument document, int targetSize, out MiniMapRenderer.MiniMapBounds bounds)
        {
            return RenderMiniMap(
                document.MapPixelWidth,
                document.MapPixelHeight,
                targetSize,
                document.S32Files,
                document.CheckedS32Files,
                out bounds);
        }

        /// <summary>
        /// 使用 MapDocument 渲染小地圖（不需要邊界資訊）
        /// </summary>
        public Bitmap RenderMiniMap(MapDocument document, int targetSize)
        {
            return RenderMiniMap(document, targetSize, out _);
        }

        /// <summary>
        /// 渲染小地圖（非同步版本）
        /// </summary>
        public Task<Bitmap> RenderMiniMapAsync(
            MapDocument document,
            int targetSize,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return null;

                return RenderMiniMap(document, targetSize);
            }, cancellationToken);
        }

        /// <summary>
        /// 渲染地圖並匯出為圖片位元組
        /// </summary>
        /// <param name="worldRect">要渲染的區域</param>
        /// <param name="document">地圖文件</param>
        /// <param name="options">渲染選項</param>
        /// <param name="format">圖片格式</param>
        /// <returns>圖片位元組陣列</returns>
        public byte[] RenderToImageBytes(
            Rectangle worldRect,
            MapDocument document,
            RenderOptions options,
            ImageFormat format)
        {
            using (var bitmap = RenderViewport(worldRect, document, options))
            {
                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, format);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// 渲染地圖並匯出為圖片位元組（非同步版本）
        /// </summary>
        public Task<byte[]> RenderToImageBytesAsync(
            Rectangle worldRect,
            MapDocument document,
            RenderOptions options,
            ImageFormat format,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return null;

                return RenderToImageBytes(worldRect, document, options, format);
            }, cancellationToken);
        }

        /// <summary>
        /// 渲染整張地圖並匯出
        /// </summary>
        public byte[] RenderFullMapToImageBytes(
            MapDocument document,
            RenderOptions options,
            ImageFormat format)
        {
            var worldRect = new Rectangle(0, 0, document.MapPixelWidth, document.MapPixelHeight);
            return RenderToImageBytes(worldRect, document, options, format);
        }

        /// <summary>
        /// 清除所有快取
        /// </summary>
        public void ClearCache()
        {
            _viewportRenderer.ClearCache();
            _miniMapRenderer.ClearCache();
        }

        /// <summary>
        /// 使指定 S32 的快取失效
        /// </summary>
        /// <param name="filePath">S32 檔案路徑</param>
        public void InvalidateS32Cache(string filePath)
        {
            _viewportRenderer.InvalidateBlockCache(filePath);
        }

        /// <summary>
        /// 處理 Tile Override 變更（清除相關快取）
        /// </summary>
        /// <param name="tileIds">變更的 TileId 列表</param>
        public void InvalidateTileCache(List<int> tileIds)
        {
            // 清除 MiniMapRenderer 的 tile 顏色快取
            _miniMapRenderer.ClearTileColorCache(tileIds);
            // 清除 MiniMapRenderer 的 S32 區塊快取（因為這些區塊可能包含變更的 tile）
            _miniMapRenderer.InvalidateS32BlockCache();
            // 清除 ViewportRenderer 的快取
            _viewportRenderer.ClearCache();
        }

        /// <summary>
        /// 取得 ViewportRenderer 的渲染統計資訊
        /// </summary>
        public ViewportRenderer.RenderStats GetLastViewportStats()
        {
            // 如果需要追蹤統計，可以在渲染時保存
            return null;
        }
    }
}
