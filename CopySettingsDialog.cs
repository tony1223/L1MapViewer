using System;
using Eto.Forms;
using Eto.Drawing;
using L1MapViewer.Compatibility;
using L1MapViewer.Localization;

namespace L1FlyMapViewer
{
    public class CopySettingsDialog : Dialog<DialogResult>
    {
        private CheckBox chkLayer1;
        private CheckBox chkLayer2;
        private CheckBox chkLayer3;
        private CheckBox chkLayer4;
        private CheckBox chkLayer5;
        private CheckBox chkLayer7;
        private CheckBox chkLayer8;
        private Button btnOK;
        private Button btnCancel;

        public bool CopyLayer1 { get; private set; }
        public bool CopyLayer2 { get; private set; }
        public bool CopyLayer3 { get; private set; }
        public bool CopyLayer4 { get; private set; }
        public bool CopyLayer5 { get; private set; }
        public bool CopyLayer7 { get; private set; }
        public bool CopyLayer8 { get; private set; }

        // 保持向後相容
        public bool CopyLayer5to8 => CopyLayer5 || CopyLayer7 || CopyLayer8;

        public CopySettingsDialog(bool currentLayer1, bool currentLayer2, bool currentLayer3, bool currentLayer4, bool currentLayer5, bool currentLayer7, bool currentLayer8)
        {
            CopyLayer1 = currentLayer1;
            CopyLayer2 = currentLayer2;
            CopyLayer3 = currentLayer3;
            CopyLayer4 = currentLayer4;
            CopyLayer5 = currentLayer5;
            CopyLayer7 = currentLayer7;
            CopyLayer8 = currentLayer8;
            BuildContent();
            LocalizationManager.LanguageChanged += OnLanguageChanged;
        }

        // 舊版建構子（向後相容）
        public CopySettingsDialog(bool currentLayer1, bool currentLayer2, bool currentLayer3, bool currentLayer4, bool currentLayer5to8)
            : this(currentLayer1, currentLayer2, currentLayer3, currentLayer4, currentLayer5to8, currentLayer5to8, currentLayer5to8)
        {
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            Application.Instance.Invoke(() => UpdateLocalization());
        }

        private void BuildContent()
        {
            Title = LocalizationManager.L("Form_CopySettings_Title");
            MinimumSize = new Size(320, 100);
            Resizable = false;

            // 建立 CheckBox
            chkLayer1 = new CheckBox { Text = LocalizationManager.L("CopySettings_Layer1_Desc"), Checked = CopyLayer1 };
            chkLayer2 = new CheckBox { Text = LocalizationManager.L("CopySettings_Layer2_Desc"), Checked = CopyLayer2 };
            chkLayer3 = new CheckBox { Text = LocalizationManager.L("CopySettings_Layer3_Desc"), Checked = CopyLayer3 };
            chkLayer4 = new CheckBox { Text = LocalizationManager.L("CopySettings_Layer4_Desc"), Checked = CopyLayer4 };
            chkLayer5 = new CheckBox { Text = LocalizationManager.L("CopySettings_Layer5_Desc"), Checked = CopyLayer5 };
            chkLayer7 = new CheckBox { Text = LocalizationManager.L("CopySettings_Layer7_Desc"), Checked = CopyLayer7 };
            chkLayer8 = new CheckBox { Text = LocalizationManager.L("CopySettings_Layer8_Desc"), Checked = CopyLayer8 };

            // 按鈕
            btnOK = new Button { Text = LocalizationManager.L("Button_OK") };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button { Text = LocalizationManager.L("Button_Cancel") };
            btnCancel.Click += (s, e) =>
            {
                Result = DialogResult.Cancel;
                Close();
            };

            // 圖層選項群組
            var layerGroup = new GroupBox { Text = LocalizationManager.L("CopySettings_SelectLayers") };
            var layerLayout = new StackLayout
            {
                Padding = new Padding(10),
                Spacing = 8,
                Items =
                {
                    chkLayer1,
                    chkLayer2,
                    chkLayer3,
                    chkLayer4,
                    chkLayer5,
                    chkLayer7,
                    chkLayer8
                }
            };
            layerGroup.Content = layerLayout;

            // 按鈕列
            var buttonLayout = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                HorizontalContentAlignment = Eto.Forms.HorizontalAlignment.Center,
                Items = { btnOK, btnCancel }
            };

            // 主布局
            var mainLayout = new StackLayout
            {
                Padding = new Padding(15),
                Spacing = 15,
                HorizontalContentAlignment = Eto.Forms.HorizontalAlignment.Stretch,
                Items =
                {
                    layerGroup,
                    buttonLayout
                }
            };

            Content = mainLayout;
            DefaultButton = btnOK;
            AbortButton = btnCancel;
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            if (chkLayer1.Checked != true && chkLayer2.Checked != true && chkLayer3.Checked != true &&
                chkLayer4.Checked != true && chkLayer5.Checked != true && chkLayer7.Checked != true && chkLayer8.Checked != true)
            {
                Eto.Forms.MessageBox.Show(this, LocalizationManager.L("Message_SelectAtLeastOneLayer"),
                    LocalizationManager.L("Title_Info"), Eto.Forms.MessageBoxType.Warning);
                return;
            }

            CopyLayer1 = chkLayer1.Checked == true;
            CopyLayer2 = chkLayer2.Checked == true;
            CopyLayer3 = chkLayer3.Checked == true;
            CopyLayer4 = chkLayer4.Checked == true;
            CopyLayer5 = chkLayer5.Checked == true;
            CopyLayer7 = chkLayer7.Checked == true;
            CopyLayer8 = chkLayer8.Checked == true;

            Result = DialogResult.Ok;
            Close();
        }

        private void UpdateLocalization()
        {
            Title = LocalizationManager.L("Form_CopySettings_Title");
            chkLayer1.Text = LocalizationManager.L("CopySettings_Layer1_Desc");
            chkLayer2.Text = LocalizationManager.L("CopySettings_Layer2_Desc");
            chkLayer3.Text = LocalizationManager.L("CopySettings_Layer3_Desc");
            chkLayer4.Text = LocalizationManager.L("CopySettings_Layer4_Desc");
            chkLayer5.Text = LocalizationManager.L("CopySettings_Layer5_Desc");
            chkLayer7.Text = LocalizationManager.L("CopySettings_Layer7_Desc");
            chkLayer8.Text = LocalizationManager.L("CopySettings_Layer8_Desc");
            btnOK.Text = LocalizationManager.L("Button_OK");
            btnCancel.Text = LocalizationManager.L("Button_Cancel");
        }

        /// <summary>
        /// 顯示對話框 (WinForms 相容)
        /// </summary>
        public DialogResult ShowDialog(Control parent)
        {
            ShowModal(parent);
            return Result;
        }
    }
}
