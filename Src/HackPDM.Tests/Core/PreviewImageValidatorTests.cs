
namespace HackPDM.Tests.Core;

public sealed class PreviewImageValidatorTests
{
	[Fact]
	public void Validate_AcceptsSupportedImageHeaders()
	{
		Assert.Null( PreviewImageValidator.Validate( [ 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 ] ) ); // PNG
		Assert.Null( PreviewImageValidator.Validate( [ 0xFF, 0xD8, 0xFF, 0xE0 ] ) ); // JPEG
		Assert.Null( PreviewImageValidator.Validate( "GIF89a\0\0"u8.ToArray() ) ); // GIF
		Assert.Null( PreviewImageValidator.Validate( "BM\0\0"u8.ToArray() ) ); // BMP
		Assert.Null( PreviewImageValidator.Validate( "RIFF\0\0\0\0WEBP"u8.ToArray() ) ); // WebP
	}

	[Fact]
	public void Validate_RejectsEmptyOversizedAndNonImagePayloads()
	{
		Assert.NotNull( PreviewImageValidator.Validate( null ) );
		Assert.NotNull( PreviewImageValidator.Validate( [] ) );
		Assert.NotNull( PreviewImageValidator.Validate( "plain text"u8.ToArray() ) );

		byte[] oversized = new byte[ PreviewImageValidator.MaxBytes + 1 ];
		new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo( oversized, 0 );
		Assert.NotNull( PreviewImageValidator.Validate( oversized ) );
	}
}
