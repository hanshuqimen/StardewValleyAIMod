using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValleyAIMod.Data;
using StardewValleyAIMod.Services;

namespace StardewValleyAIMod.Menus;

internal class AiDialogueMenu : IClickableMenu
{
    private readonly NPC _npc;
    private readonly AiService _ai;
    private readonly ConversationStore _store;
    private readonly ModSettings _settings;
    private readonly IMonitor _monitor;

    private TextBox _textBox = null!;
    private ClickableComponent _sendButton = null!;
    private ClickableComponent _closeButton = null!;

    private string _reply = "";
    private bool _busy;
    private CancellationTokenSource? _cts;

    public AiDialogueMenu(NPC npc, AiService ai, ConversationStore store, ModSettings settings, IMonitor monitor)
        : base(0, 0, 0, 0, showUpperRightCloseButton: true)
    {
        _npc = npc;
        _ai = ai;
        _store = store;
        _settings = settings;
        _monitor = monitor;

        width = Math.Min(620, Game1.uiViewport.Width - 40);
        height = 360;
        xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
        yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

        SetupWidgets();
    }

    private void SetupWidgets()
    {
        var tbX = xPositionOnScreen + 32;
        var tbY = yPositionOnScreen + 180;
        var tbTex = Game1.content.Load<Texture2D>("LooseSprites\\textBox");
        _textBox = new TextBox(tbTex, tbTex, Game1.dialogueFont, Game1.textColor)
        {
            X = tbX,
            Y = tbY,
            Width = width - 64 - 180,
            Height = 56
        };
        _textBox.Selected = true;
        Game1.keyboardDispatcher.Subscriber = _textBox;

        var sendX = xPositionOnScreen + width - 64 - 160;
        _sendButton = new ClickableComponent(
            new Rectangle(sendX, tbY - 4, 160, 60), "send")
        {
            myID = 100,
            upNeighborID = -99998,
            downNeighborID = -99998,
            leftNeighborID = -99998,
            rightNeighborID = -99998
        };

        _closeButton = new ClickableComponent(
            new Rectangle(xPositionOnScreen + width - 64 - 120, yPositionOnScreen + height - 64, 120, 50), "close")
        {
            myID = 101,
            upNeighborID = -99998,
            downNeighborID = -99998,
            leftNeighborID = -99998,
            rightNeighborID = -99998
        };
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (_closeButton.containsPoint(x, y))
        {
            Close();
            return;
        }

        if (_busy) return;

        if (_sendButton.containsPoint(x, y) && !string.IsNullOrWhiteSpace(_textBox.Text))
        {
            SendAsync(_textBox.Text).ContinueWith(_ => { });
            return;
        }

        _textBox.Selected = new Rectangle(_textBox.X, _textBox.Y, _textBox.Width, _textBox.Height).Contains(x, y);
        base.receiveLeftClick(x, y, playSound);
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape)
        {
            Close();
            return;
        }
        if (key == Keys.Enter && !string.IsNullOrWhiteSpace(_textBox.Text) && !_busy)
        {
            SendAsync(_textBox.Text).ContinueWith(_ => { });
            return;
        }
        base.receiveKeyPress(key);
    }

    private async Task SendAsync(string text)
    {
        if (_busy) return;
        _busy = true;
        _textBox.Text = "";
        _reply = "（思考中……）";
        _cts = new CancellationTokenSource();

        string systemPrompt = CharacterPrompts.GetPrompt(_npc.Name);
        if (!string.IsNullOrWhiteSpace(_settings.ExtraSystemInstruction))
            systemPrompt += "\n附加要求：" + _settings.ExtraSystemInstruction;

        try
        {
            if (_settings.SendPrimingRequest)
                await _ai.PrimeAsync(_npc.Name, systemPrompt, _cts.Token).ConfigureAwait(false);

            var history = _store.Get(_npc.Name);
            string reply = await _ai.SendAsync(_npc.Name, systemPrompt, text, history, _cts.Token)
                .ConfigureAwait(false);

            _reply = reply;
            _store.Append(_npc.Name, text, reply);

            try
            {
                _npc.CurrentDialogue.Push(new Dialogue(_npc, "AI_" + _npc.Name, reply));
            }
            catch (Exception ex)
            {
                _monitor.Log($"[AI] 推入原生对话失败（不影响菜单显示）：{ex.Message}", LogLevel.Debug);
            }
        }
        catch (Exception ex)
        {
            _reply = "（出错了：" + ex.Message + "）";
        }
        finally
        {
            _busy = false;
        }
    }

    private void Close()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _textBox.Selected = false;
        Game1.exitActiveMenu();
        Game1.playSound("bigDeSelect");
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);

        drawTextureBox(b, Game1.mouseCursors, new Rectangle(0, 256, 60, 60),
            xPositionOnScreen, yPositionOnScreen, width, height, Color.White, drawShadow: true);

        var title = $"{_npc.displayName ?? _npc.Name} · AI 对话";
        DrawText(b, title, new Vector2(xPositionOnScreen + 32, yPositionOnScreen + 28), Game1.dialogueFont, Color.PaleGoldenrod);

        var hint = CharacterPrompts.HasPrompt(_npc.Name) ? "（已加载角色人设）" : "（无专属人设，使用通用设定）";
        DrawText(b, hint, new Vector2(xPositionOnScreen + 32, yPositionOnScreen + 76), Game1.smallFont, Color.LightGray);

        var replyLabel = _busy ? "回复中……" : "回复：";
        DrawText(b, replyLabel, new Vector2(xPositionOnScreen + 32, yPositionOnScreen + 110), Game1.smallFont, Color.Wheat);
        if (!string.IsNullOrEmpty(_reply))
        {
            var wrapped = Game1.parseText(_reply, Game1.smallFont, width - 64);
            DrawText(b, wrapped, new Vector2(xPositionOnScreen + 32, yPositionOnScreen + 132), Game1.smallFont, Color.White);
        }

        _textBox.Draw(b);

        DrawButton(b, _sendButton, "发送", _busy ? Color.Gray : Color.LightGreen);
        DrawButton(b, _closeButton, "关闭", Color.Salmon);

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
}
