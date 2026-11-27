// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Amuse.App.Common
{
    /// <summary>
    /// Reads the download links and file names the add model wizards are given.
    /// </summary>
    public static class DownloadLink
    {
        /// <summary>
        /// Gets the direct download link, a Hugging Face page link is changed to its download link and the query of a Hugging Face link, such as ?download=true, is removed.
        /// </summary>
        /// <param name="link">The link.</param>
        public static string GetDownloadLink(string link)
        {
            var downloadLink = link?.Trim();
            if (!Uri.TryCreate(downloadLink, UriKind.Absolute, out var uri))
                return downloadLink;

            var isHuggingFaceFile = uri.AbsolutePath.Contains("/blob/") || uri.AbsolutePath.Contains("/resolve/");
            if (uri.Host.Equals("huggingface.co", StringComparison.OrdinalIgnoreCase) && isHuggingFaceFile)
                return new UriBuilder(uri) { Path = uri.AbsolutePath.Replace("/blob/", "/resolve/"), Query = string.Empty, Fragment = string.Empty }.Uri.AbsoluteUri;

            return uri.AbsoluteUri;
        }


        /// <summary>
        /// Gets the name the file of a download link has on disk, the escaped name the download keeps, <c>null</c> when the link is not a link.
        /// </summary>
        /// <param name="link">The link.</param>
        public static string GetLocalFileName(string link)
        {
            if (!Uri.TryCreate(GetDownloadLink(link), UriKind.Absolute, out var uri))
                return null;

            return Path.GetFileName(uri.AbsolutePath);
        }


        /// <summary>
        /// Gets the folder name for a name, the characters a folder name can't have are removed.
        /// </summary>
        /// <param name="name">The name.</param>
        public static string GetFolderName(string name)
        {
            var invalidCharacters = Path.GetInvalidFileNameChars();
            return new string([.. (name ?? string.Empty).Where(x => !invalidCharacters.Contains(x))]).Trim(' ', '.');
        }


        /// <summary>
        /// Gets the file name of a download link, <c>null</c> when the link is not a direct link to a file with one of the extensions.
        /// </summary>
        /// <param name="link">The link.</param>
        /// <param name="fileExtensions">The file extensions, with the leading dot.</param>
        public static string GetFileName(string link, string[] fileExtensions)
        {
            var downloadLink = GetDownloadLink(link);
            if (!Uri.TryCreate(downloadLink, UriKind.Absolute, out var uri))
                return null;

            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
                return null;

            if (!string.IsNullOrEmpty(uri.Query))
                return null;

            var fileName = Path.GetFileName(Uri.UnescapeDataString(uri.AbsolutePath));
            if (!IsSupportedFile(fileName, fileExtensions))
                return null;

            return fileName;
        }


        /// <summary>
        /// Determines whether a file name has one of the extensions.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        /// <param name="fileExtensions">The file extensions, with the leading dot.</param>
        public static bool IsSupportedFile(string fileName, string[] fileExtensions)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            return fileExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);
        }


        /// <summary>
        /// Gets the file picker filter for the extensions.
        /// </summary>
        /// <param name="label">The filter label.</param>
        /// <param name="fileExtensions">The file extensions, with the leading dot.</param>
        public static string GetFileFilter(string label, string[] fileExtensions)
        {
            return $"{label}|{string.Join(";", fileExtensions.Select(x => $"*{x}"))};";
        }


        /// <summary>
        /// Gets the scale factor a file name suggests, such as 4x-UltraSharp or RealESRGAN_x4plus, <c>null</c> when it suggests none.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        public static int? GetScaleFactor(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            var match = Regex.Match(Path.GetFileNameWithoutExtension(fileName), @"(?<![a-z0-9])(?:([1-8])x|x([1-8]))(?![0-9])", RegexOptions.IgnoreCase);
            if (!match.Success)
                return null;

            return int.Parse(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
        }
    }
}
