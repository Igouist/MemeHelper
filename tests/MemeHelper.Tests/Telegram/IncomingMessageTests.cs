using MemeHelper.Api.Telegram;
using Telegram.Bot.Types;

namespace MemeHelper.Tests.Telegram;

public class IncomingMessageTests
{
    private static readonly User Sender = new() { Id = 42 };
    private static readonly Chat Chat = new() { Id = 99 };

    [Fact]
    public void From_文字訊息_應為Text且帶使用者與聊天編號()
    {
        var message = new Message { From = Sender, Chat = Chat, Text = "六點 下班" };

        var incoming = IncomingMessage.From(message);

        incoming.Should().Be(new IncomingMessage.Text(42, 99, "六點 下班"));
    }

    [Fact]
    public void From_照片_應取最後一個尺寸的FileId且IsPhoto為true()
    {
        var message = new Message
        {
            From = Sender,
            Chat = Chat,
            Photo = [new PhotoSize { FileId = "small" }, new PhotoSize { FileId = "large" }],
            Caption = "六點,下班",
        };

        var incoming = IncomingMessage.From(message);

        incoming.Should().Be(new IncomingMessage.Image(42, 99, "large", null, IsPhoto: true, "六點,下班"));
    }

    [Fact]
    public void From_照片沒有說明文字_Caption應為null()
    {
        var message = new Message { From = Sender, Chat = Chat, Photo = [new PhotoSize { FileId = "p" }] };

        var incoming = IncomingMessage.From(message);

        incoming.Should().Be(new IncomingMessage.Image(42, 99, "p", null, IsPhoto: true, null));
    }

    [Fact]
    public void From_檔案_應帶檔名且IsPhoto為false()
    {
        var message = new Message
        {
            From = Sender,
            Chat = Chat,
            Document = new Document { FileId = "doc", FileName = "meme.png" },
            Caption = "神奇海螺",
        };

        var incoming = IncomingMessage.From(message);

        incoming.Should().Be(new IncomingMessage.Image(42, 99, "doc", "meme.png", IsPhoto: false, "神奇海螺"));
    }

    [Fact]
    public void From_貼圖_應忽略()
    {
        var message = new Message { From = Sender, Chat = Chat, Sticker = new Sticker { FileId = "s" } };

        IncomingMessage.From(message).Should().BeNull();
    }

    [Fact]
    public void From_沒有寄件者_應忽略()
    {
        var message = new Message { Chat = Chat, Text = "六點" };

        IncomingMessage.From(message).Should().BeNull();
    }

    [Fact]
    public void From_空的照片陣列_應忽略()
    {
        var message = new Message { From = Sender, Chat = Chat, Photo = [] };

        IncomingMessage.From(message).Should().BeNull();
    }
}
