﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using LibGit2Sharp.Core;

namespace LibGit2Sharp
{
    /// <summary>
    /// Holds the changes between two <see cref="Blob"/>s.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class ContentChanges
    {
        private readonly StringBuilder patchBuilder = new StringBuilder();

        /// <summary>
        /// Needed for mocking purposes.
        /// </summary>
        protected ContentChanges()
        { }

        internal unsafe ContentChanges(Repository repo, Blob oldBlob, Blob newBlob, GitDiffOptions options)
        {
            Proxy.git_diff_blobs(repo.Handle,
                                 oldBlob != null ? oldBlob.Id : null,
                                 newBlob != null ? newBlob.Id : null,
                                 options,
                                 FileCallback,
                                 HunkCallback,
                                 LineCallback);
        }

        internal ContentChanges(bool isBinaryComparison)
        {
            this.IsBinaryComparison = isBinaryComparison;
        }

        internal void AppendToPatch(string patch)
        {
            patchBuilder.Append(patch);
        }

        /// <summary>
        /// The number of lines added.
        /// </summary>
        public virtual int LinesAdded { get; internal set; }

        /// <summary>
        /// The number of lines deleted.
        /// </summary>
        public virtual int LinesDeleted { get; internal set; }

        /// <summary>
        /// The patch corresponding to these changes.
        /// </summary>
        public virtual string Patch
        {
            get { return patchBuilder.ToString(); }
        }

        /// <summary>
        /// Determines if at least one side of the comparison holds binary content.
        /// </summary>
        public virtual bool IsBinaryComparison { get; private set; }

        private unsafe int FileCallback(git_diff_delta* delta, float progress, IntPtr payload)
        {
            IsBinaryComparison = delta->flags.HasFlag(GitDiffFlags.GIT_DIFF_FLAG_BINARY);

            if (!IsBinaryComparison)
            {
                return 0;
            }

            AppendToPatch("Binary content differ\n");

            return 0;
        }

        private unsafe int HunkCallback(git_diff_delta* delta, GitDiffHunk hunk, IntPtr payload)
        {
            string decodedContent = LaxUtf8Marshaler.FromBuffer(hunk.Header, (int)hunk.HeaderLen);

            AppendToPatch(decodedContent);
            return 0;
        }

        private unsafe int LineCallback(git_diff_delta* delta, GitDiffHunk hunk, GitDiffLine line, IntPtr payload)
        {
            string decodedContent = LaxUtf8Marshaler.FromNative(line.content, (int)line.contentLen);

            string prefix;

            switch (line.lineOrigin)
            {
                case GitDiffLineOrigin.GIT_DIFF_LINE_ADDITION:
                    LinesAdded++;
                    prefix = Encoding.ASCII.GetString(new[] { (byte)line.lineOrigin });
                    break;

                case GitDiffLineOrigin.GIT_DIFF_LINE_DELETION:
                    LinesDeleted++;
                    prefix = Encoding.ASCII.GetString(new[] { (byte)line.lineOrigin });
                    break;

                case GitDiffLineOrigin.GIT_DIFF_LINE_CONTEXT:
                    prefix = Encoding.ASCII.GetString(new[] { (byte)line.lineOrigin });
                    break;

                default:
                    prefix = string.Empty;
                    break;
            }

            AppendToPatch(prefix);
            AppendToPatch(decodedContent);

            AddHunk(hunk, prefix + decodedContent);
            return 0;
        }

        internal void AddHunk(GitDiffHunk hunk, string line)
        {
            if (hunk == null)
                return;

            var h = Hunks.SingleOrDefault(c => c.LineStart == hunk.NewStart && c.OldLineStart == hunk.OldStart);
            if (h == null)
            {
                h = new Hunk
                {
                    OldLineStart = hunk.OldStart,
                    OldLinesLength = hunk.OldLines,
                    LineStart = hunk.NewStart,
                    LinesLength = hunk.NewLines
                };
                Hunks.Add(h);
            }

            h.AddLine(line);
        }

        private string DebuggerDisplay
        {
            get
            {
                return string.Format(CultureInfo.InvariantCulture,
                                     @"{{+{0}, -{1}}}",
                                     LinesAdded,
                                     LinesDeleted);
            }
        }

        public List<Hunk> Hunks { get; } = new List<Hunk>();
    }

    public class Hunk
    {
        public int OldLineStart { get; set; }
        public int LineStart { get; set; }
        public int OldLinesLength { get; set; }
        public int LinesLength { get; set; }

        public List<string> Lines { get; } = new List<string>();

        public void AddLine(string line)
        {
            Lines.Add(line.TrimEnd('\n'));
        }

        public override bool Equals(object obj)
        {
            if (obj is Hunk c)
                return OldLineStart == c.OldLineStart &&
                    LineStart == c.LineStart &&
                    OldLinesLength == c.OldLinesLength &&
                    LinesLength == c.LinesLength &&
                    Lines.SequenceEqual(c.Lines);
            return false;
        }
    }
}
