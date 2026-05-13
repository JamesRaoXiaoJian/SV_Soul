using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
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
    private KeyboardState _prevKeyState;

    private const int BubbleSpacing = 12;
    private const int InputHeight = 55;
    private const int PortraitSize = 64;
    private const int InputGap = 8;

    public ChatMenu(NPCAgent agent, Texture2D? portrait, IMonitor monitor, Action<string, string> onPlayerSubmit)
        : base(0, 0, 0, 0, true)
    {
        _agent = agent;
        _portrait = portrait;
        _monitor = monitor;
        _onPlayerSubmit = onPlayerSubmit;

        _textInput = new TextInputBox();
        _textInput.OnSubmit = OnPlayerMessage;

        _monitor.Log($"ChatMenu opened for {agent.DisplayName}", LogLevel.Debug);

        // Full screen overlay
        width = Game1.uiViewport.Width - 80;
        height = Game1.uiViewport.Height - 80;
        xPositionOnScreen = 40;
        yPositionOnScreen = 40;

        UpdateLayout();

        // Add greeting
        AddBubble(agent.DisplayName, $"Hey there! What's on your mind?", false);
    }

    private void UpdateLayout()
    {
        // Input box sits below the main dialogue box
        _textInput.Bounds = new Rectangle(
            xPositionOnScreen,
            yPositionOnScreen + height + InputGap,
            width,
            InputHeight
        );
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
        var bubbleWidth = Math.Min(chatArea.Width - 20, 500);
        const int textPad = 12;
        var maxTextWidth = bubbleWidth - textPad * 2;
        var wrappedLines = WrapText(text, maxTextWidth);
        const int LineHeight = 18;
        var bubbleHeight = wrappedLines.Count * LineHeight + 28;

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
        // Main dialogue box internal content area (after borders)
        var contentX = xPositionOnScreen + 20;
        var contentY = yPositionOnScreen + 20;
        var contentW = width - 40;
        var contentH = height - 40;

        // Chat area: below portrait header, above bottom of box
        var headerBottom = contentY + PortraitSize + 16;
        return new Rectangle(contentX, headerBottom, contentW, contentY + contentH - headerBottom - 8);
    }

    private List<string> WrapText(string text, int maxWidth)
    {
        var lines = new List<string>();
        var words = text.Split(' ');
        var currentLine = "";

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
            var measuredWidth = SpriteText.getWidthOfString(testLine);
            if (measuredWidth > maxWidth && !string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
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

        // Poll keyboard for text input
        if (_textInputFocused)
        {
            var keyState = Keyboard.GetState();
            var pressed = keyState.GetPressedKeys();
            foreach (var key in pressed)
            {
                if (_prevKeyState.IsKeyUp(key))
                {
                    var ch = KeyToChar(key, keyState);
                    if (ch.HasValue)
                        _textInput.HandleTextInput(ch.Value);
                }
            }
            _prevKeyState = keyState;
        }

        // Check for pending responses from the async API call
        if (_awaitingResponse && _responseQueue.TryDequeue(out var response))
        {
            _awaitingResponse = false;
            AddBubble(_agent.DisplayName, response, false);
        }
    }

    private static char? KeyToChar(Keys key, KeyboardState state)
    {
        bool shift = state.IsKeyDown(Keys.LeftShift) || state.IsKeyDown(Keys.RightShift);

        if (key >= Keys.A && key <= Keys.Z)
        {
            char c = (char)('a' + (key - Keys.A));
            return shift ? char.ToUpper(c) : c;
        }
        if (key >= Keys.D0 && key <= Keys.D9)
        {
            if (shift)
            {
                return key switch
                {
                    Keys.D1 => '!', Keys.D2 => '@', Keys.D3 => '#',
                    Keys.D4 => '$', Keys.D5 => '%', Keys.D6 => '^',
                    Keys.D7 => '&', Keys.D8 => '*', Keys.D9 => '(',
                    Keys.D0 => ')', _ => null
                };
            }
            return (char)('0' + (key - Keys.D0));
        }
        if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            return (char)('0' + (key - Keys.NumPad0));

        return key switch
        {
            Keys.Space => ' ',
            Keys.OemPeriod => shift ? '>' : '.',
            Keys.OemComma => shift ? '<' : ',',
            Keys.OemQuestion => shift ? '?' : '/',
            Keys.OemSemicolon => shift ? ':' : ';',
            Keys.OemQuotes => shift ? '"' : '\'',
            Keys.OemOpenBrackets => shift ? '{' : '[',
            Keys.OemCloseBrackets => shift ? '}' : ']',
            Keys.OemMinus => shift ? '_' : '-',
            Keys.OemPlus => shift ? '+' : '=',
            _ => null
        };
    }

    public void EnqueueResponse(string response)
    {
        _monitor.Log($"[{_agent.Name}] Enqueueing response: {response.Substring(0, Math.Min(100, response.Length))}...", LogLevel.Debug);
        _responseQueue.Enqueue(response);
    }

    public override void draw(SpriteBatch b)
    {
        // Dim background
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);

        // Main dialogue box
        Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, width, height, false, true);

        // Content area inside dialogue box borders
        var chatArea = GetChatArea();

        // NPC portrait (crop first expression from spritesheet)
        var portraitX = chatArea.X;
        var portraitY = chatArea.Y - PortraitSize - 12;

        if (_portrait != null)
        {
            var exprHeight = Math.Min(64, _portrait.Height);
            var srcRect = new Rectangle(0, 0, Math.Min(64, _portrait.Width), exprHeight);
            b.Draw(_portrait, new Rectangle(portraitX, portraitY, PortraitSize, PortraitSize), srcRect, Color.White);
        }

        // NPC name next to portrait
        SpriteText.drawString(b, _agent.DisplayName, portraitX + PortraitSize + 12, portraitY + 16);

        // Chat bubbles (scrollable, clipped)
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
                SpriteText.drawString(b, $"{_agent.DisplayName} is thinking{dots}", chatArea.X + 8, thinkingY + 8);
            }
        }

        b.End();
        b.GraphicsDevice.ScissorRectangle = prevScissor;
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

        // Input box (below main dialogue box, using drawTextureBox for clean borders)
        var inputBounds = _textInput.Bounds;
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15),
            inputBounds.X, inputBounds.Y, inputBounds.Width, inputBounds.Height, Color.White, 2f, false);
        _textInput.Draw(b);

        // Close hint
        var hintX = xPositionOnScreen + width - 170;
        var hintY = yPositionOnScreen + height - 24;
        SpriteText.drawString(b, "[Esc] to close", hintX, hintY, 999, 200, 999, 0.5f, 0.7f, false, -1, "", null, SpriteText.ScrollTextAlignment.Left);

        // Mouse cursor
        drawMouse(b);
    }

    private void DrawBubble(SpriteBatch b, ChatBubble bubble, int areaX, int areaY, int areaWidth)
    {
        var bubbleWidth = Math.Min(areaWidth - 20, 500);
        var bubbleX = bubble.IsPlayer ? areaX + areaWidth - bubbleWidth : areaX;
        const int textPad = 12;
        var textWidth = bubbleWidth - textPad * 2;

        // Bubble background
        var bgColor = bubble.IsPlayer ? new Color(100, 149, 237, 40) : new Color(60, 60, 60, 40);
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15),
            bubbleX, areaY, bubbleWidth, bubble.Height, bgColor, 2f, false);

        // Speaker name
        SpriteText.drawString(b, bubble.Speaker, bubbleX + textPad, areaY + 4, 999, textWidth, 999,
            0.6f, 0.9f, false, -1, "", null, SpriteText.ScrollTextAlignment.Left);

        // Text lines (use SpriteText scale 0.7 -> char height ~12px, line height 18px)
        const int LineHeight = 18;
        var lineY = areaY + 24;
        foreach (var line in bubble.WrappedLines)
        {
            SpriteText.drawString(b, line, bubbleX + textPad, lineY, 999, textWidth, 999,
                0.7f, 1f, false, -1, "", null, SpriteText.ScrollTextAlignment.Left);
            lineY += LineHeight;
        }
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
