namespace Playnite.Native;

public static class Winnt
{
	public const uint FILE_READ_EA = 8u;

	public const uint FILE_ATTRIBUTE_READONLY = 1u;

	public const uint FILE_ATTRIBUTE_HIDDEN = 2u;

	public const uint FILE_ATTRIBUTE_SYSTEM = 4u;

	public const uint FILE_ATTRIBUTE_DIRECTORY = 16u;

	public const uint FILE_ATTRIBUTE_ARCHIVE = 32u;

	public const uint FILE_ATTRIBUTE_DEVICE = 64u;

	public const uint FILE_ATTRIBUTE_NORMAL = 128u;

	public const uint FILE_ATTRIBUTE_TEMPORARY = 256u;

	public const uint FILE_ATTRIBUTE_SPARSE_FILE = 512u;

	public const uint FILE_ATTRIBUTE_REPARSE_POINT = 1024u;

	public const uint FILE_ATTRIBUTE_COMPRESSED = 2048u;

	public const uint FILE_ATTRIBUTE_OFFLINE = 4096u;

	public const uint FILE_ATTRIBUTE_NOT_CONTENT_INDEXED = 8192u;

	public const uint FILE_ATTRIBUTE_ENCRYPTED = 16384u;

	public const uint FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS = 4194304u;

	public const uint FILE_ATTRIBUTE_RECALL_ON_OPEN = 262144u;
}
