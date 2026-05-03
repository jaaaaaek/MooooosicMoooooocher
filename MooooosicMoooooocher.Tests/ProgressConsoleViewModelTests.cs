using MooooosicMoooooocher.ViewModels;
using Xunit;

namespace MooooosicMoooooocher.Tests
{
    public class ProgressConsoleViewModelTests
    {
        [Fact]
        public void AppendLine_AddsNewLines()
        {
            var console = new ProgressConsoleViewModel();
            console.AppendLine("first");
            console.AppendLine("second");

            Assert.Equal(2, console.Lines.Count);
            Assert.Equal("first", console.Lines[0]);
            Assert.Equal("second", console.Lines[1]);
        }

        [Fact]
        public void AppendLine_IgnoresEmptyOrWhitespace()
        {
            var console = new ProgressConsoleViewModel();
            console.AppendLine(string.Empty);
            console.AppendLine("   ");
            console.AppendLine(null!);

            Assert.Empty(console.Lines);
        }

        [Fact]
        public void UpdateOrAppendLine_FirstCall_Appends()
        {
            var console = new ProgressConsoleViewModel();
            console.UpdateOrAppendLine("0/100", "enum");

            Assert.Single(console.Lines);
            Assert.Equal("0/100", console.Lines[0]);
        }

        [Fact]
        public void UpdateOrAppendLine_SameKey_ReplacesInPlace()
        {
            var console = new ProgressConsoleViewModel();
            console.UpdateOrAppendLine("0/100", "enum");
            console.UpdateOrAppendLine("50/100", "enum");
            console.UpdateOrAppendLine("100/100", "enum");

            Assert.Single(console.Lines);
            Assert.Equal("100/100", console.Lines[0]);
        }

        [Fact]
        public void UpdateOrAppendLine_DifferentKey_AppendsNewLine()
        {
            var console = new ProgressConsoleViewModel();
            console.UpdateOrAppendLine("0/100", "enum");
            console.UpdateOrAppendLine("50/100", "enum");
            console.UpdateOrAppendLine("Lookup: 5/10", "lookup");
            console.UpdateOrAppendLine("Lookup: 10/10", "lookup");

            Assert.Equal(2, console.Lines.Count);
            Assert.Equal("50/100", console.Lines[0]);
            Assert.Equal("Lookup: 10/10", console.Lines[1]);
        }

        [Fact]
        public void AppendLine_DoesNotShiftLiveLine_NextUpdateReplacesAtTrackedIndex()
        {
            // Lines are tracked by INDEX (not "last position"), so an unrelated
            // AppendLine between two same-key UpdateOrAppendLine calls doesn't
            // break the in-place update behavior. This is what lets a live counter
            // stay anchored while interleaved status messages accumulate below.
            var console = new ProgressConsoleViewModel();
            console.UpdateOrAppendLine("first live", "key1");   // Lines = [first live]
            console.AppendLine("permanent message");             // Lines = [first live, permanent message]
            console.UpdateOrAppendLine("second live", "key1");   // replaces at index 0

            Assert.Equal(2, console.Lines.Count);
            Assert.Equal("second live", console.Lines[0]);
            Assert.Equal("permanent message", console.Lines[1]);
        }

        [Fact]
        public void Content_StaysInSyncWithLines()
        {
            var console = new ProgressConsoleViewModel();
            console.AppendLine("a");
            console.AppendLine("b");
            console.UpdateOrAppendLine("c0", "k");
            console.UpdateOrAppendLine("c1", "k");

            string expected = string.Join(Environment.NewLine, new[] { "a", "b", "c1" });
            Assert.Equal(expected, console.Content);
        }

        [Fact]
        public void Clear_RemovesAllAndResetsKey()
        {
            var console = new ProgressConsoleViewModel();
            console.UpdateOrAppendLine("x", "k");
            console.Clear();
            console.UpdateOrAppendLine("y", "k"); // should append (key was reset by Clear)

            Assert.Single(console.Lines);
            Assert.Equal("y", console.Lines[0]);
        }
    }
}
