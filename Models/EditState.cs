using System;
using System.Collections.Generic;
// using System.Drawing; // Replaced with Eto.Drawing
using System.Linq;

namespace L1MapViewer.Models
{
    /// <summary>
    /// 編輯狀態 - 管理選取、剪貼簿、Undo/Redo
    /// </summary>
    public class EditState
    {
        #region 選取狀態

        /// <summary>
        /// 是否正在選取區域
        /// </summary>
        public bool IsSelectingRegion { get; set; }

        /// <summary>
        /// 選取起始點（螢幕座標）
        /// </summary>
        public Point SelectionStart { get; set; }

        /// <summary>
        /// 選取結束點（螢幕座標）
        /// </summary>
        public Point SelectionEnd { get; set; }

        /// <summary>
        /// 目前選中的格子
        /// </summary>
        public List<SelectedCell> SelectedCells { get; set; } = new List<SelectedCell>();

        /// <summary>
        /// 選中的 Layer4 群組 ID
        /// </summary>
        public HashSet<int> SelectedLayer4Groups { get; private set; } = new HashSet<int>();

        /// <summary>
        /// 高亮顯示的 S32 資料
        /// </summary>
        public S32Data HighlightedS32Data { get; set; }

        /// <summary>
        /// 高亮顯示的格子 X 座標
        /// </summary>
        public int HighlightedCellX { get; set; } = -1;

        /// <summary>
        /// 高亮顯示的格子 Y 座標
        /// </summary>
        public int HighlightedCellY { get; set; } = -1;

        /// <summary>
        /// 選中的遊戲座標 X
        /// </summary>
        public int SelectedGameX { get; set; } = -1;

        /// <summary>
        /// 選中的遊戲座標 Y
        /// </summary>
        public int SelectedGameY { get; set; } = -1;

        #endregion

        #region 剪貼簿

        /// <summary>
        /// 是否有剪貼簿資料
        /// </summary>
        public bool HasClipboardData => CellClipboard.Count > 0;

        /// <summary>
        /// 複製的格子資料
        /// </summary>
        public List<CopiedCellData> CellClipboard { get; private set; } = new List<CopiedCellData>();

        /// <summary>
        /// 複製的 Layer2 資料
        /// </summary>
        public List<Layer2Item> Layer2Clipboard { get; private set; } = new List<Layer2Item>();

        /// <summary>
        /// 複製的 Layer5 資料
        /// </summary>
        public List<Layer5Item> Layer5Clipboard { get; private set; } = new List<Layer5Item>();

        /// <summary>
        /// 複製的 Layer6 資料
        /// </summary>
        public List<int> Layer6Clipboard { get; private set; } = new List<int>();

        /// <summary>
        /// 複製的 Layer7 資料
        /// </summary>
        public List<Layer7Item> Layer7Clipboard { get; private set; } = new List<Layer7Item>();

        /// <summary>
        /// 複製的 Layer8 資料
        /// </summary>
        public List<Layer8Item> Layer8Clipboard { get; private set; } = new List<Layer8Item>();

        /// <summary>
        /// 已啟用顯示的 Layer8 項目（點擊 marker 切換後永久顯示）
        /// Key: (S32 檔案路徑, Layer8 項目索引)
        /// </summary>
        public HashSet<(string s32Path, int index)> EnabledLayer8Items { get; set; } = new HashSet<(string, int)>();

        /// <summary>
        /// 複製區域的原點（全域座標）- 用於貼上時的基準位置
        /// </summary>
        public Point CopyRegionOrigin { get; set; }

        /// <summary>
        /// 複製來源區域的原點（全域 Layer1 座標）- 記錄複製時的起始位置
        /// </summary>
        public Point CopySourceOrigin { get; set; }

        /// <summary>
        /// 複製來源地圖 ID
        /// </summary>
        public string SourceMapId { get; set; }

        /// <summary>
        /// 是否在貼上預覽模式
        /// </summary>
        public bool IsPastePreviewMode { get; set; }

        /// <summary>
        /// 貼上預覽位置
        /// </summary>
        public Point PastePreviewLocation { get; set; }

        #endregion

        #region 複製設定

        public bool CopyLayer1 { get; set; } = true;
        public bool CopyLayer2 { get; set; } = true;
        public bool CopyLayer3 { get; set; } = true;
        public bool CopyLayer4 { get; set; } = true;
        public bool CopyLayer5to8 { get; set; } = true;

        #endregion

        #region Undo/Redo

        private const int MaxUndoHistory = 50;

        /// <summary>
        /// Undo 歷史記錄
        /// </summary>
        public Stack<UndoAction> UndoHistory { get; private set; } = new Stack<UndoAction>();

        /// <summary>
        /// Redo 歷史記錄
        /// </summary>
        public Stack<UndoAction> RedoHistory { get; private set; } = new Stack<UndoAction>();

        /// <summary>
        /// 是否可以 Undo
        /// </summary>
        public bool CanUndo => UndoHistory.Count > 0;

        /// <summary>
        /// 是否可以 Redo
        /// </summary>
        public bool CanRedo => RedoHistory.Count > 0;

        /// <summary>
        /// 推入 Undo 動作
        /// </summary>
        public void PushUndoAction(UndoAction action)
        {
            UndoHistory.Push(action);
            RedoHistory.Clear(); // 新動作清除 Redo 歷史

            // 限制歷史記錄數量
            while (UndoHistory.Count > MaxUndoHistory)
            {
                var items = UndoHistory.ToArray();
                UndoHistory.Clear();
                for (int i = 0; i < items.Length - 1; i++)
                {
                    UndoHistory.Push(items[items.Length - 2 - i]);
                }
            }

            UndoRedoChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 執行 Undo
        /// </summary>
        public UndoAction PopUndo()
        {
            if (!CanUndo) return null;
            var action = UndoHistory.Pop();
            RedoHistory.Push(action);
            UndoRedoChanged?.Invoke(this, EventArgs.Empty);
            return action;
        }

        /// <summary>
        /// 執行 Redo
        /// </summary>
        public UndoAction PopRedo()
        {
            if (!CanRedo) return null;
            var action = RedoHistory.Pop();
            UndoHistory.Push(action);
            UndoRedoChanged?.Invoke(this, EventArgs.Empty);
            return action;
        }

        /// <summary>
        /// 清除 Undo/Redo 歷史
        /// </summary>
        public void ClearUndoHistory()
        {
            UndoHistory.Clear();
            RedoHistory.Clear();
            UndoRedoChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Undo/Redo 狀態變更事件
        /// </summary>
        public event EventHandler UndoRedoChanged;

        #endregion

        #region 通行性編輯

        /// <summary>
        /// 通行性編輯模式
        /// </summary>
        public PassableEditMode PassableMode { get; set; } = PassableEditMode.None;

        /// <summary>
        /// 通行性編輯多邊形頂點
        /// </summary>
        public List<Point> PassabilityPolygonPoints { get; private set; } = new List<Point>();

        /// <summary>
        /// 是否正在繪製通行性多邊形
        /// </summary>
        public bool IsDrawingPassabilityPolygon { get; set; }

        #endregion

        #region 區域編輯

        /// <summary>
        /// 區域編輯模式
        /// </summary>
        public RegionEditMode RegionMode { get; set; } = RegionEditMode.None;

        /// <summary>
        /// 當前選擇的區域類型
        /// </summary>
        public RegionType CurrentRegionType { get; set; } = RegionType.Normal;

        /// <summary>
        /// 區域編輯多邊形頂點
        /// </summary>
        public List<Point> RegionPolygonPoints { get; private set; } = new List<Point>();

        /// <summary>
        /// 是否正在繪製區域多邊形
        /// </summary>
        public bool IsDrawingRegionPolygon { get; set; }

        #endregion

        #region Layer5 透明編輯

        /// <summary>
        /// 是否在 Layer5 透明編輯模式
        /// </summary>
        public bool IsLayer5EditMode { get; set; }

        #endregion

        #region 群組高亮顯示

        /// <summary>
        /// 群組高亮的格子列表（全域 Layer1 座標）
        /// </summary>
        public List<(int globalX, int globalY)> GroupHighlightCells { get; set; } = new List<(int, int)>();

        /// <summary>
        /// 清除群組高亮
        /// </summary>
        public void ClearGroupHighlight()
        {
            GroupHighlightCells.Clear();
        }

        #endregion

        #region 方法

        /// <summary>
        /// 清除選取
        /// </summary>
        public void ClearSelection()
        {
            SelectedCells.Clear();
            SelectedLayer4Groups.Clear();
            IsSelectingRegion = false;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 清除剪貼簿
        /// </summary>
        public void ClearClipboard()
        {
            CellClipboard.Clear();
            Layer2Clipboard.Clear();
            Layer5Clipboard.Clear();
            Layer6Clipboard.Clear();
            Layer7Clipboard.Clear();
            Layer8Clipboard.Clear();
            IsPastePreviewMode = false;
            ClipboardChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 重置所有狀態
        /// </summary>
        public void Reset()
        {
            ClearSelection();
            ClearClipboard();
            ClearUndoHistory();
            ClearGroupHighlight();
            PassableMode = PassableEditMode.None;
            PassabilityPolygonPoints.Clear();
            IsDrawingPassabilityPolygon = false;
            IsLayer5EditMode = false;
            HighlightedS32Data = null;
            HighlightedCellX = -1;
            HighlightedCellY = -1;
            SelectedGameX = -1;
            SelectedGameY = -1;
        }

        #endregion

        #region 事件

        /// <summary>
        /// 選取變更事件
        /// </summary>
        public event EventHandler SelectionChanged;

        /// <summary>
        /// 剪貼簿變更事件
        /// </summary>
        public event EventHandler ClipboardChanged;

        #endregion
    }

    /// <summary>
    /// 通行性編輯模式
    /// </summary>
    public enum PassableEditMode
    {
        None,
        SetPassable,
        SetImpassable
    }

    /// <summary>
    /// 區域編輯模式
    /// </summary>
    public enum RegionEditMode
    {
        None,           // 無區域編輯
        SetRegion       // 設置區域
    }

    /// <summary>
    /// 區域類型
    /// </summary>
    public enum RegionType
    {
        Normal,     // 一般區域（無特殊標記）
        Safe,       // 安全區域（0x02 位元）
        Combat      // 戰鬥區域（0x04 位元）
    }
}
