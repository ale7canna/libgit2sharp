using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using LibGit2Sharp.Tests.TestHelpers;
using Xunit;

namespace LibGit2Sharp.Tests
{
    public class DiffBlobToBlobFixture : BaseFixture
    {
        [Fact]
        public void ComparingABlobAgainstItselfReturnsNoDifference()
        {
            var path = SandboxStandardTestRepoGitDir();
            using (var repo = new Repository(path))
            {
                var blob = repo.Lookup<Blob>("7909961");

                ContentChanges changes = repo.Diff.Compare(blob, blob);

                Assert.Equal(0, changes.LinesAdded);
                Assert.Equal(0, changes.LinesDeleted);
                Assert.Equal(string.Empty, changes.Patch);
            }
        }

        [Fact]
        public void CanCompareTwoVersionsOfABlobWithADiffOfTwoHunks()
        {
            var path = SandboxStandardTestRepoGitDir();
            using (var repo = new Repository(path))
            {
                var oldblob = repo.Lookup<Blob>("7909961");
                var newblob = repo.Lookup<Blob>("4e935b7");

                ContentChanges changes = repo.Diff.Compare(oldblob, newblob);

                Assert.False(changes.IsBinaryComparison);

                Assert.Equal(3, changes.LinesAdded);
                Assert.Equal(1, changes.LinesDeleted);

                var expected = new StringBuilder()
                 .Append("@@ -1,4 +1,5 @@\n")
                 .Append(" 1\n")
                 .Append("+2\n")
                 .Append(" 3\n")
                 .Append(" 4\n")
                 .Append(" 5\n")
                 .Append("@@ -8,8 +9,9 @@\n")
                 .Append(" 8\n")
                 .Append(" 9\n")
                 .Append(" 10\n")
                 .Append("-12\n")
                 .Append("+11\n")
                 .Append(" 12\n")
                 .Append(" 13\n")
                 .Append(" 14\n")
                 .Append(" 15\n")
                 .Append("+16\n");

                Assert.Equal(expected.ToString(), changes.Patch);
            }
        }

        [Fact]
        public void CompareContainsInformationAboutHunks()
        {
            var path = SandboxStandardTestRepoGitDir();
            using (var repo = new Repository(path))
            {
                var oldBlob = repo.Lookup<Blob>("7909961");
                var newBlob = repo.Lookup<Blob>("4e935b7");

                var changes = repo.Diff.Compare(oldBlob, newBlob);

                var expected = new List<Hunk>
                {
                    new Hunk
                    {
                        OldLineStart = 1,
                        LineStart = 1,
                        OldLinesLength = 4,
                        LinesLength = 5,
                        OldContent = { "1", "3", "4", "5" },
                        Content = { "1", "2", "3", "4", "5" }
                    },
                    new Hunk
                    {
                        OldLineStart = 8,
                        LineStart = 9,
                        OldLinesLength = 8,
                        LinesLength = 9,
                        OldContent = { "8", "9", "10", "12", "12", "13", "14", "15"},
                        Content = { "8", "9", "10", "11", "12", "13", "14", "15", "16"}
                    }
                };

                changes.Hunks.Should().BeEquivalentTo(expected);
            }
        }

        [Fact]
        public void CompareContainsInformationAboutAddedLines()
        {
            var path = SandboxStandardTestRepoGitDir();
            using (var repo = new Repository(path))
            {
                var oldBlob = repo.Lookup<Blob>("7909961");
                var newBlob = repo.Lookup<Blob>("4e935b7");

                var changes = repo.Diff.Compare(oldBlob, newBlob);

                changes.Hunks.First().AddedLines.Should().
                    BeEquivalentTo(new List<Line>{Line.From("+2", 2)});
                changes.Hunks.Skip(1).First().AddedLines.Should().
                    BeEquivalentTo(new List<Line>{Line.From("+11", 12), Line.From("+16", 17)});
            }
        }

        [Fact]
        public void CompareContainsInformationAboutRemovedLines()
        {
            var path = SandboxStandardTestRepoGitDir();
            using (var repo = new Repository(path))
            {
                var oldBlob = repo.Lookup<Blob>("7909961");
                var newBlob = repo.Lookup<Blob>("4e935b7");

                var changes = repo.Diff.Compare(oldBlob, newBlob);

                changes.Hunks.First().RemovedLines.Should().
                    BeEquivalentTo(new List<Line>());
                changes.Hunks.Skip(1).First().RemovedLines.Should().
                    BeEquivalentTo(new List<Line>{Line.From("-12", 11)});
            }
        }

        Blob CreateBinaryBlob(IRepository repo)
        {
            string fullpath = Path.Combine(repo.Info.WorkingDirectory, "binary.bin");

            File.WriteAllBytes(fullpath, new byte[] { 17, 16, 0, 4, 65 });

            return repo.ObjectDatabase.CreateBlob(fullpath);
        }

        [Fact]
        public void CanCompareATextualBlobAgainstABinaryBlob()
        {
            string path = SandboxStandardTestRepo();
            using (var repo = new Repository(path))
            {
                Blob binBlob = CreateBinaryBlob(repo);

                var blob = repo.Lookup<Blob>("7909961");

                ContentChanges changes = repo.Diff.Compare(blob, binBlob);

                Assert.True(changes.IsBinaryComparison);

                Assert.Equal(0, changes.LinesAdded);
                Assert.Equal(0, changes.LinesDeleted);
            }
        }

        [Fact]
        public void CanCompareABlobAgainstANullBlob()
        {
            var path = SandboxStandardTestRepoGitDir();
            using (var repo = new Repository(path))
            {
                var blob = repo.Lookup<Blob>("7909961");

                ContentChanges changes = repo.Diff.Compare(null, blob);

                Assert.NotEqual(0, changes.LinesAdded);
                Assert.Equal(0, changes.LinesDeleted);
                Assert.NotEqual(string.Empty, changes.Patch);

                changes = repo.Diff.Compare(blob, null);

                Assert.Equal(0, changes.LinesAdded);
                Assert.NotEqual(0, changes.LinesDeleted);
                Assert.NotEqual(string.Empty, changes.Patch);
            }
        }

        [Fact]
        public void ComparingTwoNullBlobsReturnsAnEmptyContentChanges()
        {
            var path = SandboxStandardTestRepoGitDir();
            using (var repo = new Repository(path))
            {
                ContentChanges changes = repo.Diff.Compare((Blob)null, (Blob)null);

                Assert.False(changes.IsBinaryComparison);

                Assert.Equal(0, changes.LinesAdded);
                Assert.Equal(0, changes.LinesDeleted);
            }
        }
    }
}
