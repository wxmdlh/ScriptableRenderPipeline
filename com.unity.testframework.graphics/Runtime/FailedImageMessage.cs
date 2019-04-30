using System.IO;
using System.Text;

public class FailedImageMessage
{
    public string PathName { get; set; }

    public string ImageName { get; set; }

    public byte[] ActualImage { get; set; }

    public byte[] DiffImage { get; set; }

    public byte[] Serialize()
    {
        int capacity = PathName.Length + 4 + ImageName.Length + 4 + ActualImage.Length + 4 + (DiffImage?.Length + 4 ?? 0);
        using (var memoryStream = new MemoryStream(capacity))
        {
            using (var writer = new BinaryWriter(memoryStream))
            {
                writer.WriteString(PathName);
                writer.WriteString(ImageName);
                writer.WriteBytes(ActualImage);
                writer.WriteBytes(DiffImage);
            }

            return memoryStream.ToArray();
        }
    }

    public static FailedImageMessage Deserialize(byte[] data)
    {
        using (var messageStream = new MemoryStream(data))
        {
            using (var reader = new BinaryReader(messageStream))
            {
                return new FailedImageMessage
                {
                    PathName = reader.GetString(),
                    ImageName = reader.GetString(),
                    ActualImage = reader.GetBytes(),
                    DiffImage = reader.GetBytes(),
                };
            }
        }
    }
}

public static class BinaryWriterExtensions
{
    public static void WriteString(this BinaryWriter writer, string value, Encoding encoding = null)
    {
        encoding = encoding ?? Encoding.UTF8;
        var data = encoding.GetBytes(value);
        writer.WriteBytes(data);
    }

    public static void WriteBytes(this BinaryWriter writer, byte[] value)
    {
        if (value == null)
        {
            writer.Write(0);
            return;
        }

        writer.Write(value.Length);
        writer.Write(value);
    }
}

public static class BinaryReaderExtensions
{
    public static string GetString(this BinaryReader reader, Encoding encoding = null)
    {
        encoding = encoding ?? Encoding.UTF8;
        int length = reader.ReadInt32();
        return encoding.GetString(reader.ReadBytes(length));
    }

    public static byte[] GetBytes(this BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length <= 0)
        {
            return null;
        }

        return reader.ReadBytes(length);
    }
}
