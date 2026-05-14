using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace SV_SOUL.UI;

public class TextInputBox
{
    public string Text { get; private set; } = "";
    public Rectangle Bounds { get; set; }
    public bool IsFocused { get; set; }
    public Action<string>? OnSubmit { get; set; }

    private int _cursorPosition;
    private double _cursorBlinkTimer;
    private bool _cursorVisible = true;

    // Key repeat state
    private Keys _heldKey;
    private double _holdTimer;
    private double _repeatTimer;
    private const double HoldDelay = 400; // ms before repeat starts
    private const double RepeatRate = 50; // ms between repeats

    private const string Prefix = "You: ";
    private static readonly SpriteFont Font = Game1.smallFont;
    private static readonly float PrefixWidth = Font.MeasureString(Prefix).X;
    private const float PadLeft = 14f;

    public void HandleTextInput(char inputChar)
    {
        if (!IsFocused) return;
        if (inputChar < 32) return;
        if (Text.Length >= 200) return;

        Text = Text.Insert(_cursorPosition, inputChar.ToString());
        _cursorPosition++;
    }

    public void HandleKeyPress(Keys key)
    {
        if (!IsFocused) return;

        switch (key)
        {
            case Keys.Back:
                if (_cursorPosition > 0)
                {
                    Text = Text.Remove(_cursorPosition - 1, 1);
                    _cursorPosition--;
                }
                break;

            case Keys.Delete:
                if (_cursorPosition < Text.Length)
                    Text = Text.Remove(_cursorPosition, 1);
                break;

            case Keys.Left:
                if (_cursorPosition > 0)
                    _cursorPosition--;
                break;

            case Keys.Right:
                if (_cursorPosition < Text.Length)
                    _cursorPosition++;
                break;

            case Keys.Home:
                _cursorPosition = 0;
                break;

            case Keys.End:
                _cursorPosition = Text.Length;
                break;

            case Keys.Enter:
                if (!string.IsNullOrWhiteSpace(Text))
                {
                    OnSubmit?.Invoke(Text.Trim());
                    Text = "";
                    _cursorPosition = 0;
                }
                break;
        }
    }

    public void Update(GameTime time)
    {
        _cursorBlinkTimer += time.ElapsedGameTime.TotalMilliseconds;
        if (_cursorBlinkTimer >= 500)
        {
            _cursorBlinkTimer = 0;
            _cursorVisible = !_cursorVisible;
        }

        // Key repeat for Backspace and Delete
        if (IsFocused)
        {
            var keyState = Keyboard.GetState();
            if (keyState.IsKeyDown(_heldKey) && (_heldKey == Keys.Back || _heldKey == Keys.Delete))
            {
                var dt = time.ElapsedGameTime.TotalMilliseconds;
                _holdTimer += dt;
                if (_holdTimer >= HoldDelay)
                {
                    _repeatTimer += dt;
                    if (_repeatTimer >= RepeatRate)
                    {
                        _repeatTimer = 0;
                        HandleKeyPress(_heldKey);
                    }
                }
            }
            else
            {
                _heldKey = Keys.None;
                _holdTimer = 0;
                _repeatTimer = 0;
            }
        }
    }

    public void OnKeyDown(Keys key)
    {
        if (key == Keys.Back || key == Keys.Delete)
        {
            _heldKey = key;
            _holdTimer = 0;
            _repeatTimer = 0;
        }
    }

    public void Draw(SpriteBatch b)
    {
        var charHeight = Font.MeasureString("Xg").Y;
        var textX = Bounds.X + PadLeft + PrefixWidth + 4;
        var textY = Bounds.Y + (Bounds.Height - charHeight) / 2;
        var maxTextWidth = Bounds.Width - PadLeft - PrefixWidth - 20;

        // Draw "You: " prefix (no shadow to avoid overlap)
        b.DrawString(Font, Prefix, new Vector2(Bounds.X + PadLeft, textY), Color.DarkSlateGray);

        if (string.IsNullOrEmpty(Text) && !IsFocused)
        {
            b.DrawString(Font, "Type a message...", new Vector2(textX, textY), Color.Gray);
        }
        else
        {
            var displayText = Text;
            while (Font.MeasureString(displayText).X > maxTextWidth && displayText.Length > 1)
                displayText = displayText[1..];

            // Draw with shadow
            b.DrawString(Font, displayText, new Vector2(textX + 2, textY + 2), Color.Black * 0.3f);
            b.DrawString(Font, displayText, new Vector2(textX, textY), Game1.textColor);

            if (IsFocused && _cursorVisible)
            {
                var cursorText = Text[..Math.Min(_cursorPosition, Text.Length)];
                var cursorX = textX + Font.MeasureString(cursorText).X;
                b.Draw(Game1.staminaRect, new Rectangle((int)cursorX, (int)textY, 2, (int)charHeight), Game1.textColor);
            }
        }
    }

    public void ResetCursorBlink()
    {
        _cursorBlinkTimer = 0;
        _cursorVisible = true;
    }
}
