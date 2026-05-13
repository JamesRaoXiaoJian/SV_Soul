using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.BellsAndWhistles;

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

    public void HandleTextInput(char inputChar)
    {
        if (!IsFocused) return;
        if (inputChar < 32 || inputChar > 126) return; // printable ASCII only
        if (Text.Length >= 200) return; // max length

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
    }

    public void Draw(SpriteBatch b)
    {
        // Background is drawn by parent (ChatMenu) using drawTextureBox
        var textX = Bounds.X + 12;
        var textY = Bounds.Y + (Bounds.Height - 16) / 2;
        var maxTextWidth = Bounds.Width - 24;

        if (string.IsNullOrEmpty(Text) && !IsFocused)
        {
            // Placeholder
            SpriteText.drawString(b, "Say something...", textX, textY, 999, maxTextWidth, 999, 0.5f, 0.8f, false, -1, "", null, SpriteText.ScrollTextAlignment.Left);
        }
        else
        {
            // Text
            SpriteText.drawString(b, Text, textX, textY, 999, maxTextWidth, 999, 0.7f, 1f, false, -1, "", null, SpriteText.ScrollTextAlignment.Left);

            // Cursor - use SpriteText character width to match rendering
            if (IsFocused && _cursorVisible)
            {
                var cursorText = Text[..Math.Min(_cursorPosition, Text.Length)];
                var cursorX = textX + SpriteText.getWidthOfString(cursorText);
                var cursorHeight = (int)(SpriteText.characterHeight * 0.7f);
                b.Draw(Game1.staminaRect, new Rectangle(cursorX, textY, 2, cursorHeight), Game1.textColor);
            }
        }
    }

    public void ResetCursorBlink()
    {
        _cursorBlinkTimer = 0;
        _cursorVisible = true;
    }
}
