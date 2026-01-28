using System;
using System.Linq;
using L1MapViewer.Localization;

namespace L1FlyMapViewer
{
    /// <summary>
    /// MapForm - 多語系處理
    /// </summary>
    public partial class MapForm
    {
        // 語言變更事件處理
        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (this.GetInvokeRequired())
                this.Invoke(new Action(UpdateLocalization));
            else
                UpdateLocalization();
        }

        // 語言選單項目點擊事件
        private void LanguageMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem && menuItem.Tag is string langCode)
            {
                LocalizationManager.SetLanguage(langCode);
                UpdateLanguageMenuCheckmarks();
            }
        }

        // 更新語言選單勾選狀態
        private void UpdateLanguageMenuCheckmarks()
        {
            string currentLang = LocalizationManager.CurrentLanguage;
            langZhTWToolStripMenuItem.Checked = currentLang == "zh-TW";
            langJaJPToolStripMenuItem.Checked = currentLang == "ja-JP";
            langEnUSToolStripMenuItem.Checked = currentLang == "en-US";
        }

        // 更新所有 UI 文字
        private void UpdateLocalization()
        {
            // 頂級選單
            menuFile.Text = LocalizationManager.L("Menu_File");
            menuEdit.Text = LocalizationManager.L("Menu_Edit");
            menuView.Text = LocalizationManager.L("Menu_View");
            menuTools.Text = LocalizationManager.L("Menu_Tools");
            menuHelp.Text = LocalizationManager.L("Menu_Help");

            // 檔案選單項目
            openToolStripMenuItem.Text = LocalizationManager.L("Menu_File_OpenClient");
            importMaterialToolStripMenuItem.Text = LocalizationManager.L("Menu_Import_Material");
            importFs32ToNewMapToolStripMenuItem.Text = LocalizationManager.L("Menu_Import_Fs32ToNewMap");
            menuSaveS32.Text = LocalizationManager.L("Menu_File_Save");
            menuExportFs32.Text = LocalizationManager.L("Menu_File_ExportFs32");
            menuExportMapImage.Text = LocalizationManager.L("Menu_File_ExportMapImage");
            exportToolStripMenuItem.Text = LocalizationManager.L("Menu_Export_ServerPassability");
            exportL1JToolStripMenuItem.Text = LocalizationManager.L("Menu_Export_L1JFormat");
            exportDIRToolStripMenuItem.Text = LocalizationManager.L("Menu_Export_DIRFormat");
            menuExit.Text = LocalizationManager.L("Menu_File_Exit");

            // 編輯選單項目
            menuUndo.Text = LocalizationManager.L("Menu_Edit_Undo");
            menuRedo.Text = LocalizationManager.L("Menu_Edit_Redo");
            menuCopy.Text = LocalizationManager.L("Menu_Edit_Copy");
            menuPaste.Text = LocalizationManager.L("Menu_Edit_Paste");
            menuDelete.Text = LocalizationManager.L("Menu_Edit_Delete");
            menuBatchReplaceTile.Text = LocalizationManager.L("Menu_Edit_BatchReplaceTile");

            // 檢視選單項目
            menuReloadMap.Text = LocalizationManager.L("Menu_View_Reload");
            menuLayers.Text = LocalizationManager.L("Menu_View_Layers");
            menuLayerL1.Text = LocalizationManager.L("Menu_View_Layer1");
            menuLayerL2.Text = LocalizationManager.L("Menu_View_Layer2");
            menuLayerL4.Text = LocalizationManager.L("Menu_View_Layer4");
            menuLayerL5.Text = LocalizationManager.L("Menu_View_Layer5");
            menuLayerL8.Text = LocalizationManager.L("Menu_View_Layer8");
            menuLayerPassable.Text = LocalizationManager.L("Menu_View_Passable");
            menuLayerSafe.Text = LocalizationManager.L("Menu_View_SafeZone");
            menuLayerCombat.Text = LocalizationManager.L("Menu_View_CombatZone");
            menuLayerGrid.Text = LocalizationManager.L("Menu_View_Grid");
            menuLayerS32Bound.Text = LocalizationManager.L("Menu_View_S32Border");
            menuZoom.Text = LocalizationManager.L("Menu_View_Zoom");
            menuZoomIn.Text = LocalizationManager.L("Menu_View_ZoomIn");
            menuZoomOut.Text = LocalizationManager.L("Menu_View_ZoomOut");
            menuZoom100.Text = LocalizationManager.L("Menu_View_Zoom100");

            // 工具選單項目
            menuPassableEdit.Text = LocalizationManager.L("Menu_Tools_PassableEdit");
            menuRegionEdit.Text = LocalizationManager.L("Menu_Tools_RegionEdit");
            menuLayer5Edit.Text = LocalizationManager.L("Menu_Tools_Layer5Edit");
            menuValidateMap.Text = LocalizationManager.L("Menu_Tools_ValidateMap");
            menuCleanupTiles.Text = LocalizationManager.L("Menu_Tools_CleanupTiles");

            // 說明選單項目
            discordToolStripMenuItem.Text = LocalizationManager.L("Menu_Help_Discord");
            languageToolStripMenuItem.Text = LocalizationManager.L("Menu_Language");
            menuAbout.Text = LocalizationManager.L("Menu_Help_About");

            // 頁籤
            tabMapPreview.Text = LocalizationManager.L("Tab_MapPreview");
            tabS32Editor.Text = LocalizationManager.L("Tab_S32Editor");

            // 左下角 Tab 頁籤（使用 Eto 原生的 _leftTabs）
            if (_leftTabMap != null)
                _leftTabMap.Text = LocalizationManager.L("Tab_MapList");
            if (_leftTabS32 != null)
                _leftTabS32.Text = LocalizationManager.L("Tab_S32Files");
            txtMapSearch.PlaceholderText = LocalizationManager.L("Placeholder_SearchMap");

            // 圖層控制標籤
            chkLayer1.Text = LocalizationManager.L("Layer_1");
            chkLayer2.Text = LocalizationManager.L("Layer_2");
            chkLayer3.Text = LocalizationManager.L("Layer_3");
            chkLayer4.Text = LocalizationManager.L("Layer_4");
            chkShowPassable.Text = LocalizationManager.L("Layer_Passable");
            chkShowGrid.Text = LocalizationManager.L("Layer_Grid");
            chkShowS32Boundary.Text = LocalizationManager.L("Layer_S32Border");
            chkShowSafeZones.Text = LocalizationManager.L("Layer_SafeZones");
            chkShowCombatZones.Text = LocalizationManager.L("Layer_CombatZones");

            // S32 編輯面板按鈕
            btnReloadMap.Text = LocalizationManager.L("Button_ReloadF5");
            btnSaveS32.Text = LocalizationManager.L("Button_SaveS32");
            btnCopySettings.Text = LocalizationManager.L("Button_CopySettings");
            btnCopyMapCoords.Text = LocalizationManager.L("Button_CopyMapCoords");
            btnImportFs32.Text = LocalizationManager.L("Button_ImportFs32");
            btnEditPassable.Text = LocalizationManager.L("Button_EditPassable");
            btnEditLayer5.Text = LocalizationManager.L("Button_EditLayer5");
            btnRegionEdit.Text = LocalizationManager.L("Button_RegionEdit");

            // S32 檔案列表按鈕
            btnS32SelectAll.Text = LocalizationManager.L("Button_SelectAll");
            btnS32SelectNone.Text = LocalizationManager.L("Button_SelectNone");

            // 工具列項目
            toolStripJumpLabel.Text = LocalizationManager.L("Label_GameCoord") + ":";
            toolStripJumpButton.Text = LocalizationManager.L("Button_JumpToCoord");

            // 右側工具按鈕 - 上方工具
            btnToolCopy.Text = LocalizationManager.L("Button_Copy");
            btnToolPaste.Text = LocalizationManager.L("Button_Paste");
            btnToolDelete.Text = LocalizationManager.L("Button_Delete");
            btnToolUndo.Text = LocalizationManager.L("Button_Undo");
            btnToolRedo.Text = LocalizationManager.L("Button_Redo");
            btnToolSave.Text = LocalizationManager.L("Button_Save");
            btnToolCellInfo.Text = LocalizationManager.L("Button_Details");
            btnToolReplaceTile.Text = LocalizationManager.L("Button_Replace");
            btnToolAddS32.Text = LocalizationManager.L("Button_New");
            btnToolClearLayer7.Text = LocalizationManager.L("Button_ClearL7");
            btnToolClearCell.Text = LocalizationManager.L("Button_ClearCell");
            // 右側工具按鈕 - 下方查詢
            btnToolCheckL1.Text = LocalizationManager.L("Button_CheckL1");
            btnToolCheckL2.Text = LocalizationManager.L("Button_CheckL2");
            btnToolCheckL3.Text = LocalizationManager.L("Button_CheckL3");
            btnToolCheckL4.Text = LocalizationManager.L("Button_CheckL4");
            btnToolCheckL5.Text = LocalizationManager.L("Button_CheckL5");
            btnToolCheckL6.Text = LocalizationManager.L("Button_CheckL6");
            btnToolCheckL7.Text = LocalizationManager.L("Button_CheckL7");
            btnToolCheckL8.Text = LocalizationManager.L("Button_CheckL8");
            btnViewClipboard.Text = LocalizationManager.L("Button_ViewClipboard");
            btnToolTestTil.Text = LocalizationManager.L("Button_TestTile");
            btnToolClearTestTil.Text = LocalizationManager.L("Button_ClearTestTile");

            // 浮動圖層面板（使用 ColoredCheckBox）
            if (_layerPanelTitleLabel != null)
                _layerPanelTitleLabel.Text = "▣ " + LocalizationManager.L("Label_Layers");
            if (_layerColoredCheckBoxes != null)
            {
                foreach (var (chk, locKey) in _layerColoredCheckBoxes)
                {
                    chk.Text = LocalizationManager.L(locKey);
                }
            }

            // Tile 面板
            txtTileSearch.PlaceholderText = LocalizationManager.L("Placeholder_SearchTileId");
            lblTileList.Text = string.Format(LocalizationManager.L("Label_TileListCount"), lvTiles.Items.Count);
            lblMaterials.Text = LocalizationManager.L("Label_RecentMaterials");
            lblGroupThumbnails.Text = LocalizationManager.L("Label_GroupThumbnails");
            btnMoreMaterials.Text = LocalizationManager.L("Button_More");

            // 群組縮圖面板
            txtGroupSearch.PlaceholderText = LocalizationManager.L("Placeholder_SearchGroup");
            // 更新群組模式下拉選單
            int selectedIndex = cmbGroupMode.SelectedIndex;
            cmbGroupMode.Items.Clear();
            cmbGroupMode.Items.Add(LocalizationManager.L("GroupMode_SelectedArea"));
            cmbGroupMode.Items.Add(LocalizationManager.L("GroupMode_SelectedAreaAll"));
            cmbGroupMode.Items.Add(LocalizationManager.L("GroupMode_All"));
            cmbGroupMode.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;

            // 滑鼠操作提示現在由 DrawEditModeHelpLabelSK 在 overlay 中繪製
            // 會自動使用 LocalizationManager.L("Hint_MouseControls")

            // 狀態列
            if (toolStripStatusLabel1.Text == "就緒" || toolStripStatusLabel1.Text == "Ready" || toolStripStatusLabel1.Text == "準備完了")
                toolStripStatusLabel1.Text = LocalizationManager.L("Status_Ready");
        }
    }
}
