using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace NetControl.Windows;

public sealed class RouterOsApi : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly object _lock = new();

    public bool Connected => _client?.Connected == true;

    public async Task ConnectAsync(string host, int port, string user, string password, CancellationToken ct = default)
    {
        Disconnect();
        _client = new TcpClient { NoDelay = true };
        await _client.ConnectAsync(host, port, ct);
        _stream = _client.GetStream();

        var result = await CommandAsync("/login", new Dictionary<string, string> { ["name"] = user, ["password"] = password }, ct);
        var last = result.LastOrDefault();

        if (last is not null && last.TryGetValue("ret", out var challenge) && !string.IsNullOrEmpty(challenge))
        {
            var bytes = Convert.FromHexString(challenge);
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            using var md5 = MD5.Create();
            var input = new byte[1 + passwordBytes.Length + bytes.Length];
            input[0] = 0;
            Buffer.BlockCopy(passwordBytes, 0, input, 1, passwordBytes.Length);
            Buffer.BlockCopy(bytes, 0, input, 1 + passwordBytes.Length, bytes.Length);

            var digest = md5.ComputeHash(input);
            var hex = Convert.ToHexString(digest).ToLowerInvariant();
            result = await CommandAsync("/login", new Dictionary<string, string>
            {
                ["name"] = user,
                ["response"] = "00" + hex
            }, ct);
        }

        var done = result.LastOrDefault();
        if (done is null || (done.TryGetValue("__type", out var type) && type == "!trap"))
            throw new IOException(done?.GetValueOrDefault("message") ?? "فشل تسجيل الدخول إلى MikroTik");
    }

    public async Task<List<Dictionary<string, string>>> CommandAsync(
        string path,
        Dictionary<string, string>? args = null,
        CancellationToken ct = default)
    {
        if (_stream is null)
            throw new IOException("غير متصل");

        var tag = Guid.NewGuid().ToString("N");
        lock (_lock) { }

        await WriteWord(path, ct);
        if (args is not null)
        {
            foreach (var item in args)
                await WriteWord("=" + item.Key + "=" + (item.Value ?? ""), ct);
        }

        await WriteWord(".tag=" + tag, ct);
        await WriteWord("", ct);

        var rows = new List<Dictionary<string, string>>();
        while (true)
        {
            var row = await ReadSentence(ct);
            if (row.Count == 0)
                continue;

            rows.Add(row);
            if (row.GetValueOrDefault("__type") is "!done" or "!trap" or "!fatal")
                break;
        }

        return rows;
    }

    private async Task WriteWord(string value, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        await WriteLength(bytes.Length, ct);
        await _stream!.WriteAsync(bytes, ct);
    }

    private async Task WriteLength(int length, CancellationToken ct)
    {
        if (length < 0x80)
        {
            await _stream!.WriteByteAsync((byte)length, ct);
        }
        else if (length < 0x4000)
        {
            await _stream!.WriteByteAsync((byte)((length >> 8) | 0x80), ct);
            await _stream!.WriteByteAsync((byte)length, ct);
        }
        else if (length < 0x200000)
        {
            await _stream!.WriteByteAsync((byte)((length >> 16) | 0xC0), ct);
            await _stream!.WriteByteAsync((byte)(length >> 8), ct);
            await _stream!.WriteByteAsync((byte)length, ct);
        }
        else
        {
            throw new NotSupportedException("كلمة RouterOS كبيرة جدًا");
        }
    }

    private async Task<Dictionary<string, string>> ReadSentence(CancellationToken ct)
    {
        var data = new Dictionary<string, string>();
        while (true)
        {
            var length = await ReadLength(ct);
            if (length == 0)
                return data;

            var bytes = new byte[length];
            await ReadExact(bytes, ct);
            var word = Encoding.UTF8.GetString(bytes);

            if (word.StartsWith('!'))
                data["__type"] = word;
            else if (word.StartsWith('=') || word.StartsWith('.'))
            {
                var separator = word.IndexOf('=', 1);
                if (separator > 0)
                    data[word[1..separator]] = word[(separator + 1)..];
            }
        }
    }

    private async Task<int> ReadLength(CancellationToken ct)
    {
        var first = await ReadByte(ct);
        if ((first & 0x80) == 0) return first;
        if ((first & 0xC0) == 0x80) return ((first & 0x3F) << 8) | await ReadByte(ct);
        if ((first & 0xE0) == 0xC0) return ((first & 0x1F) << 16) | (await ReadByte(ct) << 8) | await ReadByte(ct);
        if ((first & 0xF0) == 0xE0) return ((first & 0x0F) << 24) | (await ReadByte(ct) << 16) | (await ReadByte(ct) << 8) | await ReadByte(ct);
        throw new IOException("طول RouterOS غير مدعوم");
    }

    private async Task<int> ReadByte(CancellationToken ct)
    {
        var buffer = new byte[1];
        await ReadExact(buffer, ct);
        return buffer[0];
    }

    private async Task ReadExact(byte[] buffer, CancellationToken ct)
    {
        var position = 0;
        while (position < buffer.Length)
        {
            var count = await _stream!.ReadAsync(buffer.AsMemory(position, buffer.Length - position), ct);
            if (count == 0)
                throw new EndOfStreamException();
            position += count;
        }
    }

    public void Disconnect()
    {
        try { _stream?.Dispose(); _client?.Dispose(); } catch { }
        _stream = null;
        _client = null;
    }

    public void Dispose() => Disconnect();
}
