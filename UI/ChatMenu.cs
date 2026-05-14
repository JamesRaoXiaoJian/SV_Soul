using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using SV_SOUL.Agents;

namespace SV_SOUL.UI;

public class ChatMenu : IClickableMenu
{
    private readonly NPCAgent _agent;
    private readonly Action<string, string> _onPlayerSubmit;
    private readonly Texture2D? _portrait;
    private readonly TextInputBox _textInput;
    private readonly List<ChatBubble> _bubbles = new();
    private readonly ConcurrentQueue<string> _responseQueue = new();
    private readonly IMonitor _monitor;
    private bool _awaitingResponse;
    private int _scrollOffset;
    private int _totalContentHeight;
    private bool _textInputFocused;

    private static readonly SpriteFont Font = Game1.smallFont;
    private static readonly Vector2 CharSize = Font.MeasureString("Xg");

    private const int InputBoxHeight = 65;
    private const int BottomMargin = 20;
    private const int BoxSpacing = 8;
    private const int PortraitSize = 64;
    private const int ContentPadding = 20;
    private const int BubbleSpacing = 14;
    private static readonly int LineHeight = (int)CharSize.Y + 6;

    public ChatMenu(NPCAgent agent, Texture2D? portrait, IMonitor monitor, Action<string, string> onPlayerSubmit)
        : base(0, 0, 0, 0, true)
    {
        _agent = agent;
        _portrait = portrait;
        _monitor = monitor;
        _onPlayerSubmit = onPlayerSubmit;

        _textInput = new TextInputBox();
        _textInput.OnSubmit = OnPlayerMessage;

        // Hook IME input
        Game1.game1.Window.TextInput += OnTextInput;

        _monitor.Log($"ChatMenu opened for {agent.DisplayName}", LogLevel.Debug);

        // Position at bottom center: 80% width, double height
        var viewW = Game1.uiViewport.Width;
        var viewH = Game1.uiViewport.Height;
        width = (int)(viewW * 0.8);
        height = 600;
        xPositionOnScreen = (viewW - width) / 2;
        yPositionOnScreen = viewH - height - InputBoxHeight - BoxSpacing - BottomMargin;

        UpdateLayout();

        AddBubble(agent.DisplayName, "Hey there! What's on your mind?", false);
    }

    private void UpdateLayout()
    {
        _textInput.Bounds = new Rectangle(
            xPositionOnScreen,
            yPositionOnScreen + height + BoxSpacing,
            width,
            InputBoxHeight
        );
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (_textInputFocused && e.Character >= 32)
        {
            _textInput.HandleTextInput(e.Character);
        }
    }

    private void OnPlayerMessage(string text)
    {
        AddBubble("You", text, true);
        _awaitingResponse = true;
        _onPlayerSubmit(_agent.Name, text);
    }

    private void AddBubble(string speaker, string text, bool isPlayer)
    {
        var chatArea = GetChatArea();
        var maxTextWidth = chatArea.Width - 24;
        var wrappedLines = WrapText(text, maxTextWidth);
        var bubbleHeight = wrappedLines.Count * LineHeight + 12;

        _bubbles.Add(new ChatBubble
        {
            Speaker = speaker,
            Text = text,
            WrappedLines = wrappedLines,
            IsPlayer = isPlayer,
            Height = bubbleHeight
        });

        _totalContentHeight += bubbleHeight + BubbleSpacing;
        _scrollOffset = Math.Max(0, _totalContentHeight - chatArea.Height);
    }

    private Rectangle GetChatArea()
    {
        var contentX = xPositionOnScreen + ContentPadding;
        var contentY = yPositionOnScreen + ContentPadding;
        var contentW = width - ContentPadding * 2;
        var contentH = height - ContentPadding * 2;

        var headerBottom = contentY + PortraitSize + 12;
        // Extra 16px bottom padding to prevent clipping
        return new Rectangle(contentX, headerBottom, contentW, contentY + contentH - headerBottom - 16);
    }

    private static bool IsCjk(char c)
    {
        return (c >= 0x4E00 && c <= 0x9FFF) ||   // CJK Unified
               (c >= 0x3400 && c <= 0x4DBF) ||   // CJK Extension A
               (c >= 0x3000 && c <= 0x303F) ||   // CJK Symbols
               (c >= 0xFF00 && c <= 0xFFEF) ||   // Fullwidth
               (c >= 0x3040 && c <= 0x309F) ||   // Hiragana
               (c >= 0x30A0 && c <= 0x30FF);     // Katakana
    }

    private static List<string> WrapText(string text, int maxWidth)
    {
        var lines = new List<string>();
        var currentLine = "";

        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\n')
            {
                lines.Add(currentLine);
                currentLine = "";
                continue;
            }

            if (c == ' ')
            {
                // Try to fit the next word
                var nextSpace = text.IndexOf(' ', i + 1);
                var nextBreak = nextSpace == -1 ? text.Length : nextSpace;
                var wordChunk = text[i..nextBreak];
                var testLine = currentLine + wordChunk;
                if (Font.MeasureString(testLine).X > maxWidth && currentLine.Length > 0)
                {
                    lines.Add(currentLine);
                    currentLine = "";
                    // Skip the space, start new line with the word
                    continue;
                }
                currentLine += c;
                continue;
            }

            // CJK character: break per character
            if (IsCjk(c))
            {
                var testLine = currentLine + c;
                if (Font.MeasureString(testLine).X > maxWidth && currentLine.Length > 0)
                {
                    lines.Add(currentLine);
                    currentLine = c.ToString();
                }
                else
                {
                    currentLine = testLine;
                }
                continue;
            }

            // Regular ASCII character
            {
                var testLine = currentLine + c;
                if (Font.MeasureString(testLine).X > maxWidth && currentLine.Length > 0)
                {
                    lines.Add(currentLine);
                    currentLine = c.ToString();
                }
                else
                {
                    currentLine = testLine;
                }
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
            lines.Add(currentLine);

        return lines;
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape)
        {
            Game1.exitActiveMenu();
            return;
        }

        if (_textInputFocused)
        {
            _textInput.OnKeyDown(key);
            _textInput.HandleKeyPress(key);
        }
    }

    public override void receiveScrollWheelAction(int direction)
    {
        _scrollOffset -= direction * 40;
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, _totalContentHeight - GetChatArea().Height));
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        var inputRect = _textInput.Bounds;
        if (inputRect.Contains(x, y))
        {
            _textInputFocused = true;
            _textInput.IsFocused = true;
            _textInput.ResetCursorBlink();
        }
        else
        {
            _textInputFocused = false;
            _textInput.IsFocused = false;
        }
    }

    public override void update(GameTime time)
    {
        _textInput.Update(time);

        if (_awaitingResponse && _responseQueue.TryDequeue(out var response))
        {
            _awaitingResponse = false;
            AddBubble(_agent.DisplayName, response, false);
        }
    }

    public void EnqueueResponse(string response)
    {
        _monitor.Log($"[{_agent.Name}] Enqueueing response: {response.Substring(0, Math.Min(100, response.Length))}...", LogLevel.Debug);
        _responseQueue.Enqueue(response);
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        var viewW = Game1.uiViewport.Width;
        var viewH = Game1.uiViewport.Height;
        width = (int)(viewW * 0.8);
        height = 600;
        xPositionOnScreen = (viewW - width) / 2;
        yPositionOnScreen = viewH - height - InputBoxHeight - BoxSpacing - BottomMargin;
        UpdateLayout();
    }

    public override void draw(SpriteBatch b)
    {
        // Main dialogue box at bottom center
        Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, width, height, false, true);

        var chatArea = GetChatArea();

        // NPC portrait
        var portraitX = xPositionOnScreen + ContentPadding;
        var portraitY = yPositionOnScreen + ContentPadding;

        if (_portrait != null)
        {
            var exprHeight = Math.Min(64, _portrait.Height);
            var srcRect = new Rectangle(0, 0, Math.Min(64, _portrait.Width), exprHeight);
            b.Draw(_portrait, new Rectangle(portraitX, portraitY, PortraitSize, PortraitSize), srcRect, Color.White);
        }

        // NPC name next to portrait
        var nameX = portraitX + PortraitSize + 12;
        var nameY = portraitY + 16;
        DrawText(b, _agent.DisplayName, new Vector2(nameX, nameY), Game1.textColor);

        // Chat messages (scrollable, clipped)
        var prevScissor = b.GraphicsDevice.ScissorRectangle;
        b.End();
        b.GraphicsDevice.ScissorRectangle = chatArea;
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState { ScissorTestEnable = true });

        var bubbleY = chatArea.Y - _scrollOffset;
        foreach (var bubble in _bubbles)
        {
            if (bubbleY + bubble.Height > chatArea.Y && bubbleY < chatArea.Bottom)
            {
                DrawBubble(b, bubble, chatArea.X, bubbleY, chatArea.Width);
            }
            bubbleY += bubble.Height + BubbleSpacing;
        }

        if (_awaitingResponse)
        {
            var thinkingY = bubbleY;
            if (thinkingY > chatArea.Y && thinkingY < chatArea.Bottom)
            {
                var dots = new string('.', (int)(Game1.ticks / 15) % 4);
                DrawText(b, $"{_agent.DisplayName} is thinking{dots}", new Vector2(chatArea.X + 4, thinkingY + 4), Color.Gray);
            }
        }

        b.End();
        b.GraphicsDevice.ScissorRectangle = prevScissor;
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

        // Input box
        var inputBounds = _textInput.Bounds;
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15),
            inputBounds.X, inputBounds.Y, inputBounds.Width, inputBounds.Height,
            new Color(249, 239, 200, 180), 2f, false);
        _textInput.Draw(b);

        // Close hint
        var hintX = xPositionOnScreen + width - 160;
        var hintY = inputBounds.Y + inputBounds.Height + 4;
        DrawText(b, "[Esc] to close", new Vector2(hintX, hintY), Color.Gray);

        drawMouse(b);
    }

    private void DrawBubble(SpriteBatch b, ChatBubble bubble, int areaX, int areaY, int areaWidth)
    {
        var maxTextWidth = areaWidth - 32;
        var textX = areaX + 16;

        // Speaker tag
        var speakerColor = bubble.IsPlayer ? new Color(70, 130, 220) : new Color(180, 100, 60);
        DrawText(b, $"{bubble.Speaker}:", new Vector2(textX, areaY), speakerColor);

        // Text lines
        var lineY = areaY + LineHeight;
        foreach (var line in bubble.WrappedLines)
        {
            DrawText(b, line, new Vector2(textX, lineY), Game1.textColor);
            lineY += LineHeight;
        }
    }

    private static void DrawText(SpriteBatch b, string text, Vector2 pos, Color color)
    {
        b.DrawString(Font, text, pos + new Vector2(2, 2), Color.Black * 0.3f);
        b.DrawString(Font, text, pos, color);
    }

    private class ChatBubble
    {
        public string Speaker { get; set; } = "";
        public string Text { get; set; } = "";
        public List<string> WrappedLines { get; set; } = new();
        public bool IsPlayer { get; set; }
        public int Height { get; set; }
    }
}
