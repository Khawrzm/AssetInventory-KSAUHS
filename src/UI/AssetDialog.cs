using System;
using System.Drawing;
using System.Windows.Forms;
using AssetInventory.Core;
using AssetInventory.Models;

namespace AssetInventory.UI;

internal sealed class AssetDialog : Form
{
    public Asset Result { get; private set; } = new();

    private readonly TextBox  _tbTag   = new() { MaxLength = 50 };
    private readonly TextBox  _tbDesc  = new() { MaxLength = 500 };
    private readonly TextBox  _tbLoc   = new() { MaxLength = 100 };
    private readonly TextBox  _tbMinor = new() { MaxLength = 100 };
    private readonly TextBox  _tbNote  = new() { MaxLength = 500, Multiline = true, Height = 60, ScrollBars = ScrollBars.Vertical };
    private readonly ComboBox _cbStatus = new();
    private readonly Label    _lblError = new();
    private readonly bool     _isEdit;

    public AssetDialog(Asset? existing = null)
    {
        _isEdit = existing != null;
        if (_isEdit) Result = existing!;

        Text            = _isEdit ? "تعديل أصل" : "إضافة أصل جديد";
        Size            = new Size(500, 530);
        MinimumSize     = Size;
        MaximumSize     = Size;
        StartPosition   = FormStartPosition.CenterParent;
        BackColor       = Color.White;
        Font            = Theme.FBody;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        KeyPreview      = true;
        KeyDown        += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        Build();
        if (_isEdit) PopulateFields(existing!);
    }

    private void Build()
    {
        // Header
        var header = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Theme.HeaderTop };
        header.Paint += (s, e) =>
        {
            var p = (Panel)s!;
            // Left accent bar
            e.Graphics.FillRectangle(new SolidBrush(Theme.Blue), 0, 0, 4, p.Height);
        };
        var hTitle = new Label
        {
            Text      = _isEdit ? "✎  تعديل بيانات الأصل" : "＋  إضافة أصل جديد",
            ForeColor = Color.White,
            Font      = Theme.FTitle,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(20, 0, 0, 0)
        };
        header.Controls.Add(hTitle);

        // Body
        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 16, 24, 0), BackColor = Color.White };

        _cbStatus.Items.AddRange(new object[] { "PENDING", "VERIFIED", "DISPOSED", "TRANSFERRED" });
        _cbStatus.DropDownStyle = ComboBoxStyle.DropDownList;
        _cbStatus.FlatStyle     = FlatStyle.Flat;

        var fields = new (string label, Control ctrl)[]
        {
            ("رقم TAG *",         _tbTag),
            ("الوصف *",           _tbDesc),
            ("الموقع الرئيسي",   _tbLoc),
            ("الموقع الفرعي",    _tbMinor),
            ("الحالة",            _cbStatus),
            ("ملاحظات",           _tbNote),
        };

        var layout = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize    = false,
            BackColor   = Color.White
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        foreach (var (lbl, ctrl) in fields)
        {
            var label = new Label
            {
                Text      = lbl,
                ForeColor = Theme.TextSecondary,
                Font      = Theme.FSmall,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Padding   = new Padding(0, 0, 10, 0)
            };

            Style(ctrl);
            ctrl.Dock   = DockStyle.Fill;
            ctrl.Margin = new Padding(0, 0, 0, 10);
            layout.Controls.Add(label);
            layout.Controls.Add(ctrl);
        }

        body.Controls.Add(layout);

        // Error label
        _lblError.Dock      = DockStyle.Bottom;
        _lblError.Height    = 24;
        _lblError.ForeColor = Theme.Red;
        _lblError.Font      = Theme.FSmall;
        _lblError.TextAlign = ContentAlignment.MiddleCenter;

        // Button row
        var btnRow = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Color.FromArgb(248, 250, 252) };
        btnRow.Paint += (s, e) =>
            e.Graphics.FillRectangle(new SolidBrush(Theme.Border), 0, 0, ((Panel)s!).Width, 1);

        var btnSave   = Btn(_isEdit ? "💾  حفظ التعديلات" : "✓  إضافة", Theme.Blue, Color.White);
        var btnCancel = Btn("إلغاء", Theme.Border, Theme.TextPrimary);
        btnSave.Click   += OnSave;
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        btnSave.Location   = new Point(24, 12);
        btnCancel.Location = new Point(btnSave.Right + 8, 12);
        btnRow.Controls.AddRange(new Control[] { btnSave, btnCancel });

        Controls.AddRange(new Control[] { body, _lblError, btnRow, header });
        AcceptButton = btnSave;
    }

    private static void Style(Control c)
    {
        c.BackColor = Color.FromArgb(248, 250, 252);
        c.ForeColor = Theme.TextPrimary;
        if (c is TextBox tb) tb.BorderStyle = BorderStyle.FixedSingle;
        if (c is ComboBox cb) { cb.BackColor = Color.FromArgb(248, 250, 252); cb.ForeColor = Theme.TextPrimary; }
    }

    private static Button Btn(string text, Color bg, Color fg)
    {
        var b = new Button
        {
            Text      = text,
            BackColor = bg,
            ForeColor = fg,
            FlatStyle = FlatStyle.Flat,
            Font      = Theme.FBtn,
            Size      = new Size(148, 32),
            Cursor    = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private void PopulateFields(Asset a)
    {
        _tbTag.Text          = a.TagNumber;
        _tbTag.Enabled       = false;
        _tbDesc.Text         = a.AssetDescription;
        _tbLoc.Text          = a.MajorLoc;
        _tbMinor.Text        = a.MinorLoc;
        _tbNote.Text         = a.Note;
        _cbStatus.SelectedItem = a.Status;
    }

    private void OnSave(object? s, EventArgs e)
    {
        _lblError.Text           = "";
        Result.TagNumber         = _tbTag.Text.Trim();
        Result.AssetDescription  = _tbDesc.Text.Trim();
        Result.MajorLoc          = _tbLoc.Text.Trim();
        Result.MinorLoc          = _tbMinor.Text.Trim();
        Result.Status            = _cbStatus.SelectedItem?.ToString() ?? "PENDING";
        Result.Note              = _tbNote.Text.Trim();

        if (!AssetValidator.Validate(Result, out var err)) { _lblError.Text = err; return; }

        Result.DataHash  = IntegrityGuard.CalculateRecordHash(Result.TagNumber, Result.MajorLoc);
        DialogResult     = DialogResult.OK;
        Close();
    }
}
