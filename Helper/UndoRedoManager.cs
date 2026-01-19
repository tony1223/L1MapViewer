using System;
using System.Collections.Generic;
using System.Linq;
using L1MapViewer.Models;

namespace L1MapViewer.Helper
{
    /// <summary>
    /// Undo/Redo 管理器
    /// </summary>
    public class UndoRedoManager
    {
        private const int MAX_UNDO_HISTORY = 5;

        private Stack<UndoAction> undoHistory = new Stack<UndoAction>();
        private Stack<UndoAction> redoHistory = new Stack<UndoAction>();

        public int UndoCount => undoHistory.Count;
        public int RedoCount => redoHistory.Count;

        /// <summary>
        /// 新增 Undo 記錄
        /// </summary>
        public void PushUndoAction(UndoAction action)
        {
            undoHistory.Push(action);
            redoHistory.Clear();

            // 限制歷史記錄數量
            if (undoHistory.Count > MAX_UNDO_HISTORY)
            {
                var tempStack = new Stack<UndoAction>();
                for (int i = 0; i < MAX_UNDO_HISTORY; i++)
                {
                    tempStack.Push(undoHistory.Pop());
                }
                undoHistory.Clear();
                while (tempStack.Count > 0)
                {
                    undoHistory.Push(tempStack.Pop());
                }
            }
        }

        /// <summary>
        /// 執行還原 (Ctrl+Z)
        /// </summary>
        public UndoRedoResult Undo(Dictionary<string, S32Data> allS32DataDict)
        {
            var result = new UndoRedoResult();

            if (undoHistory.Count == 0)
            {
                result.Success = false;
                result.Message = "沒有可還原的操作";
                return result;
            }

            var action = undoHistory.Pop();

            // 還原刪除的物件（重新新增）
            foreach (var objInfo in action.RemovedObjects)
            {
                if (allS32DataDict.TryGetValue(objInfo.S32FilePath, out S32Data? targetS32))
                {
                    var newObj = new ObjectTile
                    {
                        GroupId = objInfo.GroupId,
                        X = objInfo.LocalX,
                        Y = objInfo.LocalY,
                        Layer = objInfo.Layer,
                        IndexId = objInfo.IndexId,
                        TileId = objInfo.TileId
                    };
                    targetS32.Layer4.Add(newObj);
                    targetS32.IsModified = true;
                }
            }

            // 還原新增的物件（刪除）
            foreach (var objInfo in action.AddedObjects)
            {
                if (allS32DataDict.TryGetValue(objInfo.S32FilePath, out S32Data? targetS32))
                {
                    var objToRemove = targetS32.Layer4.FirstOrDefault(o =>
                        o.X == objInfo.LocalX &&
                        o.Y == objInfo.LocalY &&
                        o.GroupId == objInfo.GroupId &&
                        o.Layer == objInfo.Layer &&
                        o.IndexId == objInfo.IndexId &&
                        o.TileId == objInfo.TileId);

                    if (objToRemove != null)
                    {
                        targetS32.Layer4.Remove(objToRemove);
                        targetS32.IsModified = true;
                    }
                }
            }

            // 還原 Layer3 修改（通行性）
            foreach (var layer3Info in action.ModifiedLayer3)
            {
                if (allS32DataDict.TryGetValue(layer3Info.S32FilePath, out S32Data? targetS32))
                {
                    if (layer3Info.LocalX >= 0 && layer3Info.LocalX < 64 &&
                        layer3Info.LocalY >= 0 && layer3Info.LocalY < 64)
                    {
                        if (targetS32.Layer3[layer3Info.LocalY, layer3Info.LocalX] == null)
                        {
                            targetS32.Layer3[layer3Info.LocalY, layer3Info.LocalX] = new MapAttribute();
                        }
                        var attr = targetS32.Layer3[layer3Info.LocalY, layer3Info.LocalX];
                        attr.Attribute1 = layer3Info.OldAttribute1;
                        attr.Attribute2 = layer3Info.OldAttribute2;
                        targetS32.IsModified = true;
                    }
                }
            }

            redoHistory.Push(action);

            result.Success = true;
            result.ActionDescription = action.Description;
            result.Message = $"已還原: {action.Description} (Ctrl+Z: {undoHistory.Count} / Ctrl+Y: {redoHistory.Count})";
            return result;
        }

        /// <summary>
        /// 執行重做 (Ctrl+Y)
        /// </summary>
        public UndoRedoResult Redo(Dictionary<string, S32Data> allS32DataDict)
        {
            var result = new UndoRedoResult();

            if (redoHistory.Count == 0)
            {
                result.Success = false;
                result.Message = "沒有可重做的操作";
                return result;
            }

            var action = redoHistory.Pop();

            // 重做新增的物件（重新新增）
            foreach (var objInfo in action.AddedObjects)
            {
                if (allS32DataDict.TryGetValue(objInfo.S32FilePath, out S32Data? targetS32))
                {
                    var newObj = new ObjectTile
                    {
                        GroupId = objInfo.GroupId,
                        X = objInfo.LocalX,
                        Y = objInfo.LocalY,
                        Layer = objInfo.Layer,
                        IndexId = objInfo.IndexId,
                        TileId = objInfo.TileId
                    };
                    targetS32.Layer4.Add(newObj);
                    targetS32.IsModified = true;
                }
            }

            // 重做刪除的物件（重新刪除）
            foreach (var objInfo in action.RemovedObjects)
            {
                if (allS32DataDict.TryGetValue(objInfo.S32FilePath, out S32Data? targetS32))
                {
                    var objToRemove = targetS32.Layer4.FirstOrDefault(o =>
                        o.X == objInfo.LocalX &&
                        o.Y == objInfo.LocalY &&
                        o.GroupId == objInfo.GroupId &&
                        o.Layer == objInfo.Layer &&
                        o.IndexId == objInfo.IndexId &&
                        o.TileId == objInfo.TileId);

                    if (objToRemove != null)
                    {
                        targetS32.Layer4.Remove(objToRemove);
                        targetS32.IsModified = true;
                    }
                }
            }

            // 重做 Layer3 修改（通行性）
            foreach (var layer3Info in action.ModifiedLayer3)
            {
                if (allS32DataDict.TryGetValue(layer3Info.S32FilePath, out S32Data? targetS32))
                {
                    if (layer3Info.LocalX >= 0 && layer3Info.LocalX < 64 &&
                        layer3Info.LocalY >= 0 && layer3Info.LocalY < 64)
                    {
                        if (targetS32.Layer3[layer3Info.LocalY, layer3Info.LocalX] == null)
                        {
                            targetS32.Layer3[layer3Info.LocalY, layer3Info.LocalX] = new MapAttribute();
                        }
                        var attr = targetS32.Layer3[layer3Info.LocalY, layer3Info.LocalX];
                        attr.Attribute1 = layer3Info.NewAttribute1;
                        attr.Attribute2 = layer3Info.NewAttribute2;
                        targetS32.IsModified = true;
                    }
                }
            }

            undoHistory.Push(action);

            result.Success = true;
            result.ActionDescription = action.Description;
            result.Message = $"已重做: {action.Description} (Ctrl+Z: {undoHistory.Count} / Ctrl+Y: {redoHistory.Count})";
            return result;
        }

        /// <summary>
        /// 清除所有歷史記錄
        /// </summary>
        public void Clear()
        {
            undoHistory.Clear();
            redoHistory.Clear();
        }
    }

    /// <summary>
    /// Undo/Redo 操作結果
    /// </summary>
    public class UndoRedoResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ActionDescription { get; set; } = string.Empty;
    }
}
