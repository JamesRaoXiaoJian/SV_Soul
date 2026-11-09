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
using SV_SOUL.Models;

namespace SV_SOUL.UI;

public class ChatMenu : IClickableMenu
{
    private readonly NPCAgent _agent;
    private readonly Action<string, string> _onPlayerSubmit;
    private readonly Texture2D? _portrait;
    private readonly TextInputBox _textInput;
    private readonly List<ChatBubble> _bubbles = new();
    private readonly IMonitor _monitor;
    private readonly QuickReplyPager _choices = new();
    private readonly Func<string>? _contextProvider;
    private Rectangle[] _choiceBounds = Array.Empty<Rectangle>();
    private Rectangle _moreBounds;
    private int _choiceRows = 2;
    private bool _hasReply;
    private string _lastNpcReply="";
    private bool _controllerNavigation;
    private const int ChoiceHeight = 48;
    private bool _awaitingResponse;
    private long _viewRevision = -1;
    private int _historyLimit = 50;
    private bool _loadingOlder;
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

    public ChatMenu(NPCAgent agent, Texture2D? portrait, IMonitor monitor, Action<string, string> onPlayerSubmit, string? greeting = null, Func<string>? contextProvider = null, bool controllerInput = false)
        : base(0, 0, 0, 0, true)
    {
        _agent = agent;
        _contextProvider = contextProvider;
        _portrait = portrait;
        _monitor = monitor;
        _onPlayerSubmit = onPlayerSubmit;

        _textInput = new TextInputBox { IsChinese = agent.IsChinese };
        _textInput.OnSubmit = OnPlayerMessage;
        _controllerNavigation=controllerInput || Game1.options.gamepadControls;
        _textInput.IsFocused = _textInputFocused = !_controllerNavigation;

        // Hook IME input
        Game1.game1.Window.TextInput += OnTextInput;

        _monitor.Log($"ChatMenu opened for {agent.DisplayName}", LogLevel.Debug);

        RefreshChoices();
        UpdateLayout();

        SyncConversation();
        if (_bubbles.Count == 0) AddBubble(agent.DisplayName, greeting ?? agent.GetGreeting(), false);
    }

    private void UpdateLayout()
    {
        var viewW = Game1.uiViewport.Width;
        var viewH = Game1.uiViewport.Height;
        _choiceRows = viewH < 600 ? 1 : 2;
        _choices.SetPageSize(_choiceRows*2);
        width = Math.Min(1000, Math.Max(240, viewW - 64));
        height = Math.Min(560, Math.Max(120, viewH - InputBoxHeight - _choiceRows*(ChoiceHeight+BoxSpacing) - 80));
        xPositionOnScreen = (viewW - width) / 2;
        yPositionOnScreen = Math.Max(16, (viewH - height - InputBoxHeight - _choiceRows*(ChoiceHeight+BoxSpacing) - 48) / 2);
        _totalContentHeight = 0;
        foreach (var bubble in _bubbles)
        {
            bubble.WrappedLines = WrapText(bubble.Text, Math.Max(1, GetChatArea().Width - 32));
            bubble.Height = (bubble.WrappedLines.Count + 1) * LineHeight + 12;
            _totalContentHeight += bubble.Height + BubbleSpacing;
        }
        _scrollOffset = Math.Max(0, _totalContentHeight - GetChatArea().Height);
        _textInput.Bounds = new Rectangle(
            xPositionOnScreen,
            yPositionOnScreen + height + BoxSpacing,
            width - 120,
            InputBoxHeight
        );
        _moreBounds = new Rectangle(_textInput.Bounds.Right+8,_textInput.Bounds.Y,112,InputBoxHeight);
        _choiceBounds = Enumerable.Range(0,_choiceRows*2).Select(i=>new Rectangle(xPositionOnScreen+(i%2)*((width-8)/2+8),_textInput.Bounds.Bottom+8+(i/2)*(ChoiceHeight+8),(width-8)/2,ChoiceHeight)).ToArray();
    }

    private void RefreshChoices()
    {
        string world="{}";
        try { world=_contextProvider?.Invoke() ?? "{}"; }
        catch(Exception ex){_monitor.Log("Choice context unavailable: "+ex.GetType().Name,LogLevel.Debug);}
        _choices.SetItems(QuickReplies.Create(_agent.Personality,world,_agent.IsChinese,_hasReply,_lastNpcReply));
    }
    private void FocusChoices() { _textInputFocused=false;_textInput.IsFocused=false; }
    private void SendChoice()
    {
        if(_awaitingResponse)return;
        if(_choices.Current is { } choice)SubmitTestMessage(choice.Text);
    }
    public bool HandleControllerButton(string button)
    {
        _controllerNavigation=true;
        switch(button)
        {
            case "ControllerB": Game1.exitActiveMenu();return true;
            case "ControllerA": FocusChoices();SendChoice();return true;
            case "ControllerY": FocusChoices();_choices.NextPage();return true;
            case "ControllerX":
                if(!_awaitingResponse && !string.IsNullOrWhiteSpace(_textInput.Text))
                {
                    _textInput.IsFocused=true;_textInput.HandleKeyPress(Keys.Enter);FocusChoices();
                }
                return true;
            case "DPadLeft": case "LeftThumbstickLeft": FocusChoices();_choices.Move(-1,0);return true;
            case "DPadRight": case "LeftThumbstickRight": FocusChoices();_choices.Move(1,0);return true;
            case "DPadUp": case "LeftThumbstickUp": FocusChoices();_choices.Move(0,-1);return true;
            case "DPadDown": case "LeftThumbstickDown": FocusChoices();_choices.Move(0,1);return true;
            case "LeftShoulder": _scrollOffset=Math.Max(0,_scrollOffset-Math.Max(40,GetChatArea().Height/2));return true;
            case "RightShoulder": _scrollOffset=Math.Min(Math.Max(0,_totalContentHeight-GetChatArea().Height),_scrollOffset+Math.Max(40,GetChatArea().Height/2));return true;
            case "RightThumbstickUp": receiveScrollWheelAction(1);return true;
            case "RightThumbstickDown": receiveScrollWheelAction(-1);return true;
            default:return false;
        }
    }
    public object DescribeControls()=>new { Page=_choices.Page, Pages=_choices.PageCount, Selected=_choices.Selected, Choices=_choices.Visible, Waiting=_awaitingResponse, GamepadConnected=GamePad.GetState(PlayerIndex.One).IsConnected, Panel=new {X=xPositionOnScreen,Y=yPositionOnScreen,Width=width,Height=height}, Input=_textInput.Bounds, Buttons=_choiceBounds };
    // SMAPI routes and suppresses controller buttons, avoiding a second native A-click.
    public override void receiveGamePadButton(Buttons button) { }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (_textInputFocused && e.Character >= 32)
        {
            _textInput.HandleTextInput(e.Character);
        }
    }

    public bool SubmitTestMessage(string text)
    {
        if (_awaitingResponse || string.IsNullOrWhiteSpace(text)) return false;
        OnPlayerMessage(text);
        return true;
    }

    private void OnPlayerMessage(string text)
    {
        if(_awaitingResponse || string.IsNullOrWhiteSpace(text))return;
        if (!_agent.BeginRequest(text)) return;
        AddBubble(_agent.IsChinese ? "你" : "You", text, true);
        _awaitingResponse = true;
        _textInput.CanSubmit = false;
        _onPlayerSubmit(_agent.Name, text);
    }

    private void AddBubble(string speaker, string text, bool isPlayer)
    {
        var chatArea = GetChatArea();
        var maxTextWidth = Math.Max(1, chatArea.Width - 32);
        var wrappedLines = WrapText(text, maxTextWidth);
        var bubbleHeight = (wrappedLines.Count + 1) * LineHeight + 12;

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
        return new Rectangle(contentX, headerBottom, contentW, Math.Max(1,contentY + contentH - headerBottom - 16));
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

    protected override void cleanupBeforeExit()
    {
        Game1.game1.Window.TextInput -= OnTextInput;
        base.cleanupBeforeExit();
    }

    public override void receiveKeyPress(Keys key)
    {
        _controllerNavigation=false;
        if (key == Keys.Escape)
        {
            Game1.exitActiveMenu();
            return;
        }

        if(key==Keys.Tab) { _textInputFocused=!_textInputFocused;_textInput.IsFocused=_textInputFocused;return; }
        if(key==Keys.PageUp){receiveScrollWheelAction(1);return;}
        if(key==Keys.PageDown){receiveScrollWheelAction(-1);return;}
        if(!_textInputFocused)
        {
            if(key==Keys.Left)_choices.Move(-1,0);
            if(key==Keys.Right)_choices.Move(1,0);
            if(key==Keys.Up)_choices.Move(0,-1);
            if(key==Keys.Down)_choices.Move(0,1);
            if(key==Keys.Enter)SendChoice();
            if(key==Keys.Space)_choices.NextPage();
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
        _scrollOffset -= Math.Sign(direction) * 40;
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, _totalContentHeight - GetChatArea().Height));
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        _controllerNavigation=false;
        if(_moreBounds.Contains(x,y)){FocusChoices();_choices.NextPage();return;}
        for(int i=0;i<_choices.Visible.Count;i++)
            if(_choiceBounds[i].Contains(x,y)){FocusChoices();_choices.Select(i);SendChoice();return;}
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

        if (_scrollOffset == 0 && _bubbles.Count >= _historyLimit) { _historyLimit += 50; _viewRevision = -1; _loadingOlder=true; }
        SyncConversation();
    }

    private void SyncConversation()
    {
        if (_agent.ViewRevision == _viewRevision) return;
        var view = _agent.ConversationView(_historyLimit);
        if (view.Revision == _viewRevision) return;
        var oldHeight = _totalContentHeight;
        var oldOffset = _scrollOffset;
        var bottom = oldHeight == 0 || oldOffset >= oldHeight - GetChatArea().Height - 8;
        _viewRevision = view.Revision;
        _bubbles.Clear(); _totalContentHeight = 0;
        foreach (var turn in view.Messages)
            AddBubble(turn.Role == "user" ? (_agent.IsChinese ? "你" : "You") : _agent.DisplayName, turn.Content, turn.Role == "user");
        if (view.Draft != null) AddBubble(_agent.IsChinese ? "你" : "You", view.Draft, true);
        if (view.Error != null) AddBubble(_agent.IsChinese ? "请求状态" : "Request status", view.Error, false);
        _awaitingResponse = view.Pending; _textInput.CanSubmit = !view.Pending;
        _lastNpcReply = view.Messages.LastOrDefault(t => t.Role == "assistant")?.Content ?? "";
        _hasReply = _lastNpcReply.Length > 0;
        if (!bottom || _loadingOlder) _scrollOffset = Math.Max(0, oldOffset + (_loadingOlder ? _totalContentHeight - oldHeight : 0));
        _loadingOlder=false;
        RefreshChoices();
    }

    public void EnqueueResponse(string response)
    {
        _monitor.Log($"[{_agent.Name}] Enqueueing response: {response.Substring(0, Math.Min(100, response.Length))}...", LogLevel.Debug);
        // Replies are observed through the shared conversation revision.
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        UpdateLayout();
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.35f);
        // Main dialogue box at bottom center
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15),
            xPositionOnScreen, yPositionOnScreen, width, height, Color.White, 4f, false);

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

        if (_awaitingResponse)
            DrawText(b, _agent.IsChinese ? "正在思考…" : "Thinking…", new Vector2(nameX, nameY + LineHeight), Color.Gray);

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

        b.End();
        b.GraphicsDevice.ScissorRectangle = prevScissor;
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

        // Input box
        var inputBounds = _textInput.Bounds;
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15),
            inputBounds.X, inputBounds.Y, inputBounds.Width, inputBounds.Height,
            new Color(249, 239, 200, 180), 2f, false);
        _textInput.Draw(b);

        IClickableMenu.drawTextureBox(b,Game1.mouseCursors,new Rectangle(384,396,15,15),_moreBounds.X,_moreBounds.Y,_moreBounds.Width,_moreBounds.Height,Color.White,2f,false);
        DrawText(b,_agent.IsChinese?"换话题":"More",new Vector2(_moreBounds.X+12,_moreBounds.Y+18),Game1.textColor);
        var visible=_choices.Visible;
        for(int i=0;i<visible.Count;i++)
        {
            var bounds=_choiceBounds[i];
            var tint=_awaitingResponse?new Color(205,198,180):!_textInputFocused&&i==_choices.Selected?new Color(255,217,120):new Color(249,239,200);
            IClickableMenu.drawTextureBox(b,Game1.mouseCursors,new Rectangle(384,396,15,15),bounds.X,bounds.Y,bounds.Width,bounds.Height,tint,2f,false);
            var label=visible[i].Text;
            while(label.Length>1 && Font.MeasureString(label+"…").X>bounds.Width-24)label=label[..^1];
            if(label!=visible[i].Text)label+="…";
            DrawText(b,label,new Vector2(bounds.X+12,bounds.Y+(bounds.Height-CharSize.Y)/2),_awaitingResponse?Color.Gray:Game1.textColor);
        }
        var hint=_agent.IsChinese?$"A 发送  B 关闭  Y 换页  LB/RB 滚动  {_choices.Page+1}/{_choices.PageCount}":$"A Send  B Close  Y Next  LB/RB Scroll  {_choices.Page+1}/{_choices.PageCount}";
        if(!_controllerNavigation)hint=_agent.IsChinese?$"Tab 切换输入/选项  回车发送  空格换页  Esc 关闭  {_choices.Page+1}/{_choices.PageCount}":$"Tab Focus  Enter Send  Space Next  Esc Close  {_choices.Page+1}/{_choices.PageCount}";
        while(hint.Length>0 && Font.MeasureString(hint).X>width)hint=hint[..^1];
        DrawText(b,hint,new Vector2(xPositionOnScreen,_choiceBounds[^1].Bottom+6),Color.LightGray);

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
