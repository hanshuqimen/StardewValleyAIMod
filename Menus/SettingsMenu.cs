using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValleyAIMod.Services;

namespace StardewValleyAIMod.Menus;

/// <summary>
/// 游戏内的 AI 设置窗口。玩家无需改代码、无需配置环境，安装 mod 后进游戏按设置键
/// 即可在此填写"您选用的 AI 的网址""您的 API Key""模型名称"等，保存后即用。
/// mod 只读取这里的输入并转发请求，不内置任何密钥。
/// </summary>
internal class SettingsMenu : IClickableMenu
{
    private readonly ModSettings _shared;      // 供 AiService 使用的共享实例（保存时回写）
    private readonly SettingsStore _store;
    private readonly AiService _ai;
    private readonly IMonitor _monitor;
    private readonly ModSettings _edit;        // 窗口内编辑用的临时副本

    private readonly List<Field> _fields = new();
    private int _activeIndex;
    private ClickableComponent _testBtn = null!;
    private ClickableComponent _saveBtn = null!;
    private ClickableComponent _cancelBtn = null!;

    private string _status = "请填写后点击「保存」。";
    private Color _statusColor = Color.LightGray;
    private bool _testing;
    private CancellationTokenSource? _cts;

    public SettingsMenu(ModSettings shared, SettingsStore store, AiService ai, IMonitor monitor)
        : base(0, 0, 0, 0, showUpperRightCloseButton: false)
    {
        _shared = shared;
        _store = store;
        _ai = ai;
        _monitor = monitor;
        _edit = shared.Clone();

        width = Math.Min(720, Game1.uiViewport.Width - 40);
        height = 540;
        xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
        yPositionOnScreen = Math.Max(16, (Game1.uiViewport.Height - height) / 2);

        BuildFields();
        BuildButtons();
        SetActive(0);
    }

    private void BuildFields()
    {
        var tbTex = Game1.content.Load<Texture2D>("LooseSprites\\textBox");
        int tbW = width - 80;
        int x = xPositionOnScreen + 40;
        int y = yPositionOnScreen + 96;

        AddField("您选用的 AI 的网址是：", _edit.ApiUrl, 240,
            "例: https://api.openai.com/v1/chat/completions");
        AddField("您的 API 是：", _edit.ApiKey, 240, "sk-... （仅本机保存，不上传）");
        AddField("模型名称：", _edit.Model, 120, "例: gpt-3.5-turbo");
        AddField("附加指令（可选）：", _edit.ExtraSystemInstruction, 240,
            "对回复风格的要求，如「请用简短中文回复」");

        void AddField(string label, string initial, int textLimit, string placeholder)
        {
            var box = new TextBox(tbTex, tbTex, Game1.smallFont, Game1.textColor)
            {
                X = x,
                Y = y + 28,
                Width = tbW,
                Height = 48,
                Text = initial ?? ""
            };
            _fields.Add(new Field { Label = label, Box = box, Placeholder = placeholder });
            y += 28 + 48 + 24; // label + box + gap
        }
    }

    private void BuildButtons()
    {
        int btnY = yPositionOnScreen + height - 96;
        int btnW = 150, btnH = 52, gap = 16;
        int totalW = btnW * 3 + gap * 2;
        int startX = xPositionOnScreen + (width - totalW) / 2;

        _testBtn = Make(startX, btnY, btnW, btnH, 1);
        _saveBtn = Make(startX + (btnW + gap), btnY, btnW, btnH, 2);
        _cancelBtn = Make(startX + (btnW + gap) * 2, btnY, btnW, btnH, 3);

        static ClickableComponent Make(int bx, int by, int bw, int bh, int id) =>
            new(new Rectangle(bx, by, bw, bh), id.ToString())
            {
                myID = id,
                upNeighborID = -99998,
                downNeighborID = -99998,
                leftNeighborID = -99998,
                rightNeighborID = -99998
            };
    }

    private void SetActive(int index)
    {
        if (_fields.Count == 0) return;
        _activeIndex = Math.Clamp(index, 0, _fields.Count - 1);
        for (int i = 0; i < _fields.Count; i++)
            _fields[i].Box.Selected = (i == _activeIndex);
        Game1.keyboardDispatcher.Subscriber = _fields[_activeIndex].Box;
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        // 取消始终可用
        if (_cancelBtn.containsPoint(x, y)) { Close(); return; }

        // 测试/保存在测试中禁用
        if (!_testing)
        {
            if (_saveBtn.containsPoint(x, y)) { Save(); return; }
            if (_testBtn.containsPoint(x, y)) { _ = TestAsync(); return; }
        }

        // 点击某个输入框则切换激活
        for (int i = 0; i < _fields.Count; i++)
        {
            var b = _fields[i].Box;
            if (new Rectangle(b.X, b.Y, b.Width, b.Height).Contains(x, y))
            {
                SetActive(i);
                return;
            }
        }
        base.receiveLeftClick(x, y, playSound);
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape) { Close(); return; }
        if (key == Keys.Tab) { SetActive((_activeIndex + 1) % _fields.Count); return; }
        if (key == Keys.Enter)
        {
            // 回车在测试/保存之间切换体验：未配置则保存，已配置则测试
            if (!_testing) Save();
            return;
        }
        base.receiveKeyPress(key);
    }

    private void Save()
    {
        // 把窗口里输入的内容回写到共享实例，并持久化
        _edit.ApiUrl = _fields[0].Box.Text ?? "";
        _edit.ApiKey = _fields[1].Box.Text ?? "";
        _edit.Model = _fields[2].Box.Text ?? "";
        _edit.ExtraSystemInstruction = _fields[3].Box.Text ?? "";

        _shared.ApiUrl = _edit.ApiUrl;
        _shared.ApiKey = _edit.ApiKey;
        _shared.Model = string.IsNullOrWhiteSpace(_edit.Model) ? _shared.Model : _edit.Model;
        _shared.ExtraSystemInstruction = _edit.ExtraSystemInstruction;

        _store.Save(_shared);

        _statusColor = _shared.IsValid ? Color.LightGreen : Color.Salmon;
        _status = _shared.IsValid
            ? "已保存！现在走到 NPC 旁按 L 即可对话。"
            : "已保存，但网址为空，仍无法对话。";
        Game1.playSound(_shared.IsValid ? "reward" : "cancel");
    }

    private async Task TestAsync()
    {
        if (_testing) return;
        _testing = true;
        _status = "正在测试连接……";
        _statusColor = Color.LightGray;
        _cts = new CancellationTokenSource();

        string url = _fields[0].Box.Text ?? "";
        string key = _fields[1].Box.Text ?? "";
        string model = _fields[2].Box.Text ?? "";

        try
        {
            var (ok, msg) = await _ai.TestAsync(url, key, model, _cts.Token).ConfigureAwait(false);
            _statusColor = ok ? Color.LightGreen : Color.Salmon;
            _status = (ok ? "✓ " : "✗ ") + msg;
        }
        catch (Exception ex)
        {
            _statusColor = Color.Salmon;
            _status = "✗ " + ex.Message;
        }
        finally
        {
            _testing = false;
        }
    }

    private void Close()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        Game1.keyboardDispatcher.Subscriber = null;
        Game1.exitActiveMenu();
        Game1.playSound("bigDeSelect");
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.55f);

        drawTextureBox(b, Game1.mouseCursors, new Rectangle(0, 256, 60, 60),
            xPositionOnScreen, yPositionOnScreen, width, height, Color.White, drawShadow: true);

        // 标题
        DrawText(b, "AI 对话设置", new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 24),
            Game1.dialogueFont, Color.PaleGoldenrod);
        DrawText(b, "mod 只做请求中转：下面填写的网址和 Key 会原样转发给你接入的 AI，不内置任何密钥。",
            new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 60), Game1.smallFont, Color.LightGray);

        // 字段
        foreach (var f in _fields)
        {
            DrawText(b, f.Label, new Vector2(f.Box.X, f.Box.Y - 26), Game1.smallFont, Color.Wheat);
            f.Box.Draw(b);
            // 空且未聚焦时画占位提示
            if (string.IsNullOrEmpty(f.Box.Text) && !f.Box.Selected && !string.IsNullOrEmpty(f.Placeholder))
                DrawText(b, f.Placeholder, new Vector2(f.Box.X + 8, f.Box.Y + 10), Game1.smallFont, Color.DarkGray);
        }

        // 按钮
        DrawButton(b, _testBtn, "测试连接", _testing ? Color.Gray : Color.LightSkyBlue);
        DrawButton(b, _saveBtn, "保存", _testing ? Color.Gray : Color.LightGreen);
        DrawButton(b, _cancelBtn, "取消", Color.Salmon);

        // 状态行
        DrawText(b, _status,
            new Vector2(xPositionOnScreen + 40, yPositionOnScreen + height - 36),
            Game1.smallFont, _statusColor);

        drawMouse(b);
    }

    private static void DrawText(SpriteBatch b, string text, Vector2 pos, SpriteFont font, Color color)
    {
        b.DrawString(font, text, pos + new Vector2(1, 1), Color.Black * 0.4f);
        b.DrawString(font, text, pos, color);
    }

    private void DrawButton(SpriteBatch b, ClickableComponent cc, string label, Color tint)
    {
        drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 63, 63),
            cc.bounds.X, cc.bounds.Y, cc.bounds.Width, cc.bounds.Height, tint, drawShadow: false);
        var size = Game1.smallFont.MeasureString(label);
        b.DrawString(Game1.smallFont, label,
            new Vector2(cc.bounds.Center.X - size.X / 2f, cc.bounds.Center.Y - size.Y / 2f),
            Color.Black);
    }

    private class Field
    {
        public string Label = "";
        public TextBox Box = null!;
        public string Placeholder = "";
    }
}
