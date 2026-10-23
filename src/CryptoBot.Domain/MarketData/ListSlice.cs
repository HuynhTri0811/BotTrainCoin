using System.Collections;

namespace CryptoBot.Domain.MarketData;

/// <summary>
/// Một đoạn liên tiếp của danh sách có sẵn, không sao chép phần tử. Dùng cho cửa sổ nến trượt:
/// mỗi nến chỉ tạo một view nhỏ thay vì copy 200–300 tham chiếu như <c>List.GetRange</c>.
/// Danh sách gốc không được thay đổi trong lúc view còn dùng.
/// </summary>
public sealed class ListSlice<T> : IReadOnlyList<T>
{
    private readonly IReadOnlyList<T> _source;
    private readonly int _start;

    public ListSlice(IReadOnlyList<T> source, int start, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (start + count > source.Count)
            throw new ArgumentOutOfRangeException(nameof(count), "Đoạn vượt quá danh sách gốc.");

        _source = source;
        _start = start;
        Count = count;
    }

    public int Count { get; }

    public T this[int index] => (uint)index < (uint)Count
        ? _source[_start + index]
        : throw new ArgumentOutOfRangeException(nameof(index));

    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
            yield return _source[_start + i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
