using ColorCode;
using Markdig;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Collections.Generic;
using System.Net;
using System.Windows.Media;

namespace Amuse.App.Services
{
    public static class MarkdownConverter
    {
        private static readonly MarkdownPipeline _pipeline;
        private static readonly HtmlClassFormatter _formatter;
        private static readonly string _htmlStyleSheet;
        private static readonly string _htmlJavascript;

        /// <summary>
        /// Initializes static members of the <see cref="MarkdownConverter"/> class.
        /// </summary>
        static MarkdownConverter()
        {
            _htmlStyleSheet = App.GetEmbeddedResource("Amuse.App.Controls.MarkdownElement.css");
            _htmlJavascript = App.GetEmbeddedResource("Amuse.App.Controls.MarkdownElement.js");
            _formatter = new HtmlClassFormatter();
            _pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .DisableHtml()
                .UseSmartyPants()
                .UseEmojiAndSmiley()
                .UseSoftlineBreakAsHardlineBreak()
                .Use(new ConversationExtension())
                .Use(new ColorCodeExtension(_formatter))
                .Build();
        }


        /// <summary>
        /// Builds an HTML page with styles and scripts.
        /// </summary>
        /// <param name="markdown">The markdown.</param>
        /// <param name="fontSize">Size of the font.</param>
        /// <param name="fontFamily">The font family.</param>
        /// <param name="isThinkingVisible">if set to <c>true</c> [is thinking visible].</param>
        public static string BuildHtml(string markdown, double fontSize, FontFamily fontFamily, bool isThinkingVisible)
        {
            var htmlBody = BuildBody(markdown, isThinkingVisible);
            return FormatHtmlPage(htmlBody, fontSize, fontFamily);
        }


        /// <summary>
        /// Builds an empty HTML page with styles and scripts ready for streaming updates
        /// </summary>
        /// <param name="fontSize">Size of the font.</param>
        /// <param name="fontFamily">The font family.</param>
        public static string BuildEmptyHtml(double fontSize, FontFamily fontFamily)
        {
            return FormatHtmlPage(string.Empty, fontSize, fontFamily);
        }


        /// <summary>
        /// Builds an HTML page without styles and scripts.
        /// </summary>
        /// <param name="markdown">The markdown.</param>
        public static string BuildCleanHtml(string markdown)
        {
            var bodyContent = BuildBody(markdown, true);
            if (bodyContent.Contains(ColorCodeBlockRenderer.Button))
                bodyContent = bodyContent.Replace(ColorCodeBlockRenderer.Button, string.Empty);

            return FormatCleanHtmlPage(bodyContent);
        }


        /// <summary>
        /// Builds an HTML body.
        /// </summary>
        /// <param name="markdown">The markdown.</param>
        /// <param name="isThinkingVisible">if set to <c>true</c> [is thinking visible].</param>
        public static string BuildBody(string markdown, bool isThinkingVisible)
        {
            if (string.IsNullOrEmpty(markdown))
                return string.Empty;

            var markdownHtml = Markdown.ToHtml(markdown, _pipeline);
            if (isThinkingVisible && markdownHtml.Contains(ThinkBlockRenderer.ContainerClosed))
                markdownHtml = markdownHtml.Replace(ThinkBlockRenderer.ContainerClosed, ThinkBlockRenderer.ContainerOpened);

            return markdownHtml;
        }


        public static string FormatHtmlPage(ReadOnlySpan<char> bodyContent, double fontSize, FontFamily fontFamily)
        {
            return $$"""
            <!DOCTYPE html>
            <html>
                <head>
                    <meta charset="UTF-8">
                    <script>
                        {{_htmlJavascript}}
                    </script>
                    <style>
                        body,p,span,pre,li,th,td {
                            font-size:{{fontSize}}px;
                            font-family:"{{fontFamily.Source}}";
                        }
                        {{_htmlStyleSheet}}
                    </style>
                </head>
                <body>
                    {{bodyContent}}
                </body>
            </html>
            """;
        }


        public static string FormatCleanHtmlPage(ReadOnlySpan<char> bodyContent)
        {
            return $$"""
            <!DOCTYPE html>
            <html>
                <head>
                    <meta charset="UTF-8">
                </head>
                <body>
                    {{bodyContent}}
                </body>
            </html>
            """;
        }

    }


    public sealed class ConversationExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline)
        {
            if (!pipeline.BlockParsers.Contains<ConversationBlockParser>())
                pipeline.BlockParsers.Insert(0, new ConversationBlockParser());
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer is HtmlRenderer htmlRenderer)
            {
                if (!htmlRenderer.ObjectRenderers.Contains<TagBlockRenderer>())
                    htmlRenderer.ObjectRenderers.Add(new TagBlockRenderer());
                if (!htmlRenderer.ObjectRenderers.Contains<ThinkBlockRenderer>())
                    htmlRenderer.ObjectRenderers.Add(new ThinkBlockRenderer());
                if (!htmlRenderer.ObjectRenderers.Contains<HiddenBlockRenderer>())
                    htmlRenderer.ObjectRenderers.Add(new HiddenBlockRenderer());
            }
        }
    }


    public sealed class ConversationBlockParser : BlockParser
    {
        private readonly string[] _stripTags;

        public ConversationBlockParser()
        {
            OpeningCharacters = ['<'];
            _stripTags = ["<assistant>", "</assistant>"];
        }

        public override BlockState TryOpen(BlockProcessor processor)
        {
            if (processor.Line.Match("<think>"))
            {
                processor.NewBlocks.Push(new ThinkBlock(this, "Thinking...", "</think>"));
                processor.GoToColumn(processor.Line.End + 1);
                return BlockState.ContinueDiscard;
            }
            if (processor.Line.Match("<user>"))
            {
                processor.NewBlocks.Push(new TagBlock(this, "User", "</user>"));
                processor.GoToColumn(processor.Line.End + 1);
                return BlockState.ContinueDiscard;
            }
            if (processor.Line.Match("<system>"))
            {
                processor.NewBlocks.Push(new HiddenBlock(this, "System", "</system>"));
                processor.GoToColumn(processor.Line.End + 1);
                return BlockState.ContinueDiscard;
            }
            if (processor.Line.Match("<context>"))
            {
                processor.NewBlocks.Push(new HiddenBlock(this, "Context", "</context>"));
                processor.GoToColumn(processor.Line.End + 1);
                return BlockState.ContinueDiscard;
            }

            foreach (var stripTags in _stripTags)
            {
                if (processor.Line.Match(stripTags))
                {
                    processor.GoToColumn(processor.Line.End + 1);
                    return BlockState.None;
                }
            }
            return BlockState.None;
        }


        public override BlockState TryContinue(BlockProcessor processor, Block block)
        {
            if (block is ThinkBlock thinkBlock && processor.Line.Match(thinkBlock.CloseTag))
            {
                processor.GoToColumn(processor.Line.End + 1);
                return BlockState.BreakDiscard;
            }
            if (block is TagBlock tagblock && processor.Line.Match(tagblock.CloseTag))
            {
                processor.GoToColumn(processor.Line.End + 1);
                return BlockState.BreakDiscard;
            }
            if (block is HiddenBlock hiddenBlock && processor.Line.Match(hiddenBlock.CloseTag))
            {
                processor.GoToColumn(processor.Line.End + 1);
                return BlockState.BreakDiscard;
            }
            return BlockState.Continue;
        }
    }


    public sealed class ThinkBlock : ContainerBlock
    {
        public ThinkBlock(BlockParser parser, string label, string closeTag) : base(parser)
        {
            Label = label;
            CloseTag = closeTag;
        }
        public string Label { get; }
        public string CloseTag { get; }
    }


    public sealed class ThinkBlockRenderer : HtmlObjectRenderer<ThinkBlock>
    {
        protected override void Write(HtmlRenderer renderer, ThinkBlock block)
        {
            renderer.WriteLine(ContainerClosed);
            renderer.WriteLine($"<summary class=\"thinking-summary\">{block.Label}</summary>");
            renderer.WriteLine("<div>");
            renderer.WriteChildren(block);
            renderer.WriteLine("</div>");
            renderer.Write("</details>");
        }

        public const string ContainerOpened = "<details class=\"thinking-panel\" open>";
        public const string ContainerClosed = "<details class=\"thinking-panel\">";
    }


    public sealed class TagBlock : ContainerBlock
    {
        public TagBlock(BlockParser parser, string name, string closeTag) : base(parser)
        {
            Name = name;
            CloseTag = closeTag;
            ClassName = $"message-{Name.ToLowerInvariant()}";
        }

        public string Name { get; }
        public string CloseTag { get; }
        public string ClassName { get; }
    }


    public sealed class TagBlockRenderer : HtmlObjectRenderer<TagBlock>
    {
        protected override void Write(HtmlRenderer renderer, TagBlock block)
        {
            Block lastProcessedBlock = null;
            renderer.WriteLine($"<div class=\"{block.ClassName}\">");
            foreach (var subBlock in block.Descendants<Block>())
            {
                if (subBlock is HiddenBlock hiddenBlock)
                {
                    HiddenBlockRenderer.WriteBlock(renderer, hiddenBlock);
                    continue;
                }

                if (subBlock.Parent is HiddenBlock)
                    continue;

                if (subBlock is CodeBlock codeBlock)
                {
                    if (lastProcessedBlock != null)
                        renderer.EnsureLine();

                    lastProcessedBlock = subBlock;
                    foreach (var line in codeBlock.Lines.Lines)
                    {
                        if (line.Slice.Length > 0)
                        {
                            renderer.WriteEscape(line.Slice);
                            if (line.NewLine != Markdig.Helpers.NewLine.None)
                                renderer.EnsureLine();
                        }
                    }
                    continue;
                }

                if (subBlock is LeafBlock leafBlock && leafBlock.Inline != null)
                {
                    if (lastProcessedBlock != null && lastProcessedBlock != subBlock)
                        renderer.EnsureLine();

                    lastProcessedBlock = subBlock;
                    Inline inline = leafBlock.Inline;
                    while (inline != null)
                    {
                        RenderInlineFiltered(renderer, inline);
                        inline = inline.NextSibling;
                    }
                }
            }
            renderer.WriteLine();
            renderer.WriteLine("</div>");
        }


        private static void RenderInlineFiltered(HtmlRenderer renderer, Inline inline)
        {
            switch (inline)
            {
                case LiteralInline:
                    renderer.Write(inline);
                    break;
                case LinkInline:
                case AutolinkInline:
                    renderer.Write(inline);
                    break;
                case CodeInline codeInline:
                    renderer.WriteEscape(codeInline.Content);
                    break;
                case LineBreakInline:
                    renderer.EnsureLine();
                    break;
                case ContainerInline container:
                    var child = container.FirstChild;
                    bool isOpen = false;
                    while (child != null)
                    {
                        if (child is LiteralInline && !isOpen)
                        {
                            isOpen = true;
                            renderer.Write("<p>");
                        }
                        RenderInlineFiltered(renderer, child);
                        child = child.NextSibling;
                        if (child is not LiteralInline && isOpen)
                        {
                            isOpen = false;
                            renderer.Write("</p>");
                        }
                    }
                    break;
            }
        }
    }


    public sealed class HiddenBlock : ContainerBlock
    {
        public HiddenBlock(BlockParser parser, string name, string closeTag) : base(parser)
        {
            Name = name;
            CloseTag = closeTag;
            ClassName = $"message-{Name.ToLowerInvariant()}";
        }

        public string Name { get; }
        public string CloseTag { get; }
        public string ClassName { get; }
    }


    public sealed class HiddenBlockRenderer : HtmlObjectRenderer<HiddenBlock>
    {
        protected override void Write(HtmlRenderer renderer, HiddenBlock block)
        {
            WriteBlock(renderer, block);
        }

        public static void WriteBlock(HtmlRenderer renderer, HiddenBlock block)
        {
            renderer.WriteLine($"<div class=\"{block.ClassName}\">");
            renderer.WriteChildren(block);
            renderer.WriteLine("</div>");
        }
    }


    public sealed class ColorCodeExtension : IMarkdownExtension
    {
        private readonly HtmlClassFormatter _formatter;

        public ColorCodeExtension(HtmlClassFormatter formatter)
        {
            _formatter = formatter;
        }

        public void Setup(MarkdownPipelineBuilder pipeline) { }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer is HtmlRenderer htmlRenderer)
            {
                var defaultRenderer = htmlRenderer.ObjectRenderers.Find<CodeBlockRenderer>();
                if (defaultRenderer != null)
                {
                    htmlRenderer.ObjectRenderers.Remove(defaultRenderer);
                }
                htmlRenderer.ObjectRenderers.Add(new ColorCodeBlockRenderer(_formatter));
            }
        }
    }


    public sealed class ColorCodeBlockRenderer : HtmlObjectRenderer<CodeBlock>
    {
        private readonly HtmlClassFormatter _formatter;

        public ColorCodeBlockRenderer(HtmlClassFormatter formatter)
        {
            _formatter = formatter;
        }

        public const string Button = "<button class=\"copy-code\">📋</button>";

        protected override void Write(HtmlRenderer renderer, CodeBlock block)
        {
            var codeContent = block.Lines.ToString();
            var codeLanguage = GetCodeLanguage(block);
            if (codeLanguage == null)
            {
                renderer.Write(FormatCodeSection($"<pre>{WebUtility.HtmlEncode(codeContent)}</pre>"));
                return;
            }

            var formattedHtmlSpan = _formatter.GetHtmlString(codeContent, codeLanguage).AsSpan();
            var codeSectionStart = formattedHtmlSpan.IndexOf("<pre>");
            var codeSectionEnd = formattedHtmlSpan.LastIndexOf("</pre>") + 6;
            if (codeSectionEnd > codeSectionStart)
                formattedHtmlSpan = FormatCodeSection(formattedHtmlSpan[codeSectionStart..codeSectionEnd]);

            renderer.Write(formattedHtmlSpan);
        }


        private static string FormatCodeSection(ReadOnlySpan<char> codeContent)
        {
            return $$"""
            <div class="copy-block">
                <div class="copy-content">
                    {{codeContent}}
                </div>
                {{Button}}
            </div>
            """;
        }


        private static ILanguage GetCodeLanguage(CodeBlock codeBlock)
        {
            try
            {
                var language = GetCodeBlockLanguage(codeBlock);
                if (string.IsNullOrEmpty(language))
                    return null;

                if (AlternateLanguages.TryGetValue(language, out var alternateLanguage))
                    return Languages.FindById(alternateLanguage);

                return Languages.FindById(language);
            }
            catch (Exception)
            {
                return null;
            }
        }


        private static string GetCodeBlockLanguage(CodeBlock codeBlock)
        {
            if (codeBlock is not FencedCodeBlock fencedCodeBlock || string.IsNullOrEmpty(fencedCodeBlock.Info))
                return string.Empty;

            var info = fencedCodeBlock.Info.AsSpan();
            int end = info.IndexOfAny(' ', '\t');
            if (end >= 0)
                info = info[..end];
            return info.Trim().ToString();
        }


        /// <summary>
        /// The alternate language mapping
        /// </summary>
        private static readonly Dictionary<string, string> AlternateLanguages = new()
        {
            {"md", "markdown" },
            {"jsx", "html" },
            {"tsx", "html" },
            {"bash", "powershell" },
            {"kotlin", "csharp" },
            {"rust", "cplusplus" }
        };
    }
}
