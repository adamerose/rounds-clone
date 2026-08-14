using System.Diagnostics;
using System.Text.Json;
using Rounds.Replay;
using Rounds.Sim;
using Rounds.Sim.Math;

namespace Rounds.Sim.Tests;

public sealed class GoldenHistoryScriptTests
{
    private static readonly string Repository = FindRepository();
    private static readonly string HistoryScript = Path.Combine(Repository, "tools", "checks", "check-golden-history.ps1");
    private static readonly string EventScript = Path.Combine(Repository, "tools", "checks", "check-golden-event.ps1");
    private static readonly string CiEventScript = Path.Combine(Repository, "tools", "checks", "check-ci-golden-event.ps1");

    [Fact]
    public void RootCommitTreatsGoldenAsValidatedAddition()
    {
        using var fixture = new GitFixture();
        fixture.WriteEmptyLedger();
        fixture.WriteReplay("root-golden", 1);
        var root = fixture.Commit("root");

        var result = fixture.PowerShell(HistoryScript, "-Base", "ROOT", "-Head", root, "-Repository", fixture.Root);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("absent", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void MultiCommitPushCannotHideEarlierUnledgeredReplacement()
    {
        using var fixture = GitFixture.WithInitialReplay("protected", 1);
        fixture.WriteReplay("baseline", 9);
        var root = fixture.Commit("add retained baseline");
        fixture.WriteReplay("protected", 2);
        fixture.Commit("unledgered replacement");
        fixture.WriteText("unrelated.txt", "later\n");
        var head = fixture.Commit("later commit");

        var result = fixture.Event(root, root, head);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("same-commit ledger entry", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletionWithoutLedgerAndMissingRevisionFailClosed()
    {
        using var fixture = GitFixture.WithInitialReplay("protected", 1);
        var root = fixture.Head;
        fixture.DeleteReplay("protected");
        var deleted = fixture.Commit("unledgered deletion");

        var deletion = fixture.PowerShell(HistoryScript, "-Base", root, "-Head", deleted, "-Repository", fixture.Root);
        var missing = fixture.PowerShell(HistoryScript, "-Base", root, "-Head", new string('f', 40), "-Repository", fixture.Root);

        Assert.NotEqual(0, deletion.ExitCode);
        Assert.Contains("same-commit ledger entry", deletion.Output, StringComparison.Ordinal);
        Assert.NotEqual(0, missing.ExitCode);
        Assert.Contains("rev-parse", missing.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WrongDuplicateOrOrphanLedgerTransitionsFailClosed()
    {
        using (var wrong = GitFixture.WithInitialReplay("protected", 1))
        {
            var root = wrong.Head;
            wrong.WriteReplay("protected", 2);
            var newHash = wrong.ReplayHash("protected");
            wrong.AppendLedger("protected", "0000000000000000", newHash, "wrong old hash");
            var head = wrong.Commit("wrong ledger hash");
            var result = wrong.PowerShell(HistoryScript, "-Base", root, "-Head", head, "-Repository", wrong.Root);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("same-commit ledger entry", result.Output, StringComparison.Ordinal);
        }

        using (var duplicate = GitFixture.WithInitialReplay("protected", 1))
        {
            var root = duplicate.Head;
            var oldHash = duplicate.ReplayHash("protected");
            duplicate.WriteReplay("protected", 2);
            var newHash = duplicate.ReplayHash("protected");
            duplicate.AppendLedger("protected", oldHash, newHash, "first duplicate");
            duplicate.AppendLedger("protected", oldHash, newHash, "second duplicate");
            var head = duplicate.Commit("duplicate ledger tuple");
            var result = duplicate.PowerShell(HistoryScript, "-Base", root, "-Head", head, "-Repository", duplicate.Root);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("duplicates transition", result.Output, StringComparison.Ordinal);
        }

        using (var orphan = GitFixture.WithInitialReplay("protected", 1))
        {
            var root = orphan.Head;
            var hash = orphan.ReplayHash("protected");
            orphan.AppendLedger("protected", hash, "1111111111111111", "orphan transition");
            var head = orphan.Commit("orphan ledger tuple");
            var result = orphan.PowerShell(HistoryScript, "-Base", root, "-Head", head, "-Repository", orphan.Root);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("orphan transition", result.Output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EditingAnExistingLedgerLineFailsClosed()
    {
        using var fixture = GitFixture.WithInitialReplay("protected", 1);
        var root = fixture.Head;
        var oldHash = fixture.ReplayHash("protected");
        fixture.WriteReplay("protected", 2);
        var newHash = fixture.ReplayHash("protected");
        fixture.AppendLedger("protected", oldHash, newHash, "original rationale");
        fixture.Commit("valid replacement");
        fixture.WriteText(
            "replays/intentional-breaks.md",
            $"# Intentional replay breaks\n\n- replay: protected{ReplayFormat.FileSuffix}; old: {oldHash}; new: {newHash}; reason: rewritten rationale\n");
        var head = fixture.Commit("edit old ledger line");

        var result = fixture.Event(root, root, head);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("edited, reordered, or truncated", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplacementAndDeletionFormValidatedChain()
    {
        using var fixture = GitFixture.WithInitialReplay("protected", 1);
        fixture.WriteReplay("baseline", 9);
        var root = fixture.Commit("add retained baseline");
        var oldHash = fixture.ReplayHash("protected");
        fixture.WriteReplay("protected", 2);
        var middleHash = fixture.ReplayHash("protected");
        fixture.AppendLedger("protected", oldHash, middleHash, "test replacement");
        fixture.Commit("replace");
        fixture.DeleteReplay("protected");
        fixture.AppendLedger("protected", middleHash, "deleted", "test deletion");
        var head = fixture.Commit("delete");

        var result = fixture.Event(root, root, head);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains($"{oldHash} {middleHash}", result.Output, StringComparison.Ordinal);
        Assert.Contains($"{middleHash} deleted", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedBasenameCannotBeReadded()
    {
        using var fixture = GitFixture.WithInitialReplay("reserved", 1);
        var root = fixture.Head;
        var oldHash = fixture.ReplayHash("reserved");
        fixture.DeleteReplay("reserved");
        fixture.AppendLedger("reserved", oldHash, "deleted", "reserve name");
        fixture.Commit("delete");
        fixture.WriteReplay("reserved", 2);
        var head = fixture.Commit("readd");

        var result = fixture.PowerShell(HistoryScript, "-Base", root, "-Head", head, "-Repository", fixture.Root);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("permanently reserved", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void ShallowHistoryFailsExplicitly()
    {
        using var source = GitFixture.WithInitialReplay("shallow", 1);
        source.WriteText("later.txt", "later\n");
        source.Commit("later");
        using var clone = GitFixture.CloneShallow(source.Root);

        var result = clone.PowerShell(HistoryScript, "-Base", "ROOT", "-Head", "HEAD", "-Repository", clone.Root);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("shallow repository", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LedgerRejectsBlankEntryLines()
    {
        using var fixture = GitFixture.WithInitialReplay("baseline", 1);
        var oldHash = fixture.ReplayHash("baseline");
        fixture.WriteReplay("baseline", 2);
        var newHash = fixture.ReplayHash("baseline");
        fixture.WriteText(
            "replays/intentional-breaks.md",
            $"# Intentional replay breaks\n\n\n- replay: baseline{ReplayFormat.FileSuffix}; old: {oldHash}; new: {newHash}; reason: intentional test change\n");
        var changed = fixture.Commit("malformed ledger");

        var result = fixture.PowerShell(HistoryScript, "-Base", changed + "^", "-Head", changed, "-Repository", fixture.Root);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("ledger", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CleanTwoParentMergeCarriesSideHistoryWithoutNewTransition()
    {
        using var fixture = GitFixture.WithInitialReplay("merge-golden", 1);
        var root = fixture.Head;
        var oldHash = fixture.ReplayHash("merge-golden");
        fixture.Git("switch", "-c", "feature");
        fixture.WriteReplay("merge-golden", 2);
        var newHash = fixture.ReplayHash("merge-golden");
        fixture.AppendLedger("merge-golden", oldHash, newHash, "feature transition");
        fixture.Commit("feature changes golden");
        fixture.Git("switch", "main");
        fixture.WriteText("main.txt", "main\n");
        fixture.Commit("main");
        fixture.Git("merge", "--no-edit", "feature");
        var head = fixture.Head;

        var result = fixture.PowerShell(HistoryScript, "-Base", root, "-Head", head, "-Repository", fixture.Root);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains($"{oldHash} {newHash}", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void EventChainsAdditionAndTwoReplacements()
    {
        using var fixture = GitFixture.WithInitialReplay("baseline", 1);
        var root = fixture.Head;
        fixture.WriteReplay("chain", 1);
        fixture.Commit("add chain golden");
        var first = fixture.ReplayHash("chain");
        fixture.WriteReplay("chain", 2);
        var second = fixture.ReplayHash("chain");
        fixture.AppendLedger("chain", first, second, "first change");
        fixture.Commit("first change");
        fixture.WriteReplay("chain", 3);
        var third = fixture.ReplayHash("chain");
        fixture.AppendLedger("chain", second, third, "second change");
        var head = fixture.Commit("second change");

        var result = fixture.Event(root, root, head);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("golden event passed", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void ProspectiveEventPreservesGoldenAddedOnlyToAdvancedBase()
    {
        using var fixture = GitFixture.WithInitialReplay("baseline", 1);
        var root = fixture.Head;
        fixture.Git("switch", "-c", "feature");
        fixture.WriteText("feature.txt", "feature\n");
        var feature = fixture.Commit("feature");
        fixture.Git("switch", "main");
        fixture.WriteReplay("base-only", 2);
        var established = fixture.Commit("base adds golden");

        var result = fixture.Event(root, established, feature, prospective: true);

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void ProspectiveEventRejectsSamePathMergeConflict()
    {
        using var fixture = GitFixture.WithInitialReplay("contested", 1);
        var root = fixture.Head;
        var original = fixture.ReplayHash("contested");
        fixture.Git("switch", "-c", "feature");
        fixture.WriteReplay("contested", 2);
        var featureHash = fixture.ReplayHash("contested");
        fixture.AppendLedger("contested", original, featureHash, "feature change");
        var feature = fixture.Commit("feature changes golden");
        fixture.Git("switch", "main");
        fixture.WriteReplay("contested", 3);
        var baseHash = fixture.ReplayHash("contested");
        fixture.AppendLedger("contested", original, baseHash, "base change");
        var established = fixture.Commit("base changes same golden");

        var result = fixture.Event(root, established, feature, prospective: true);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("merge conflict", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProspectiveEventRejectsReservedNameResurrectionFromOldFork()
    {
        using var fixture = GitFixture.WithInitialReplay("baseline", 1);
        var root = fixture.Head;
        fixture.Git("switch", "-c", "feature");
        fixture.WriteReplay("reserved-future", 3);
        var feature = fixture.Commit("old fork adds future reserved name");
        fixture.Git("switch", "main");
        fixture.WriteReplay("reserved-future", 2);
        fixture.Commit("base adds name");
        var deletedHash = fixture.ReplayHash("reserved-future");
        fixture.DeleteReplay("reserved-future");
        fixture.AppendLedger("reserved-future", deletedHash, "deleted", "reserve after deletion");
        var established = fixture.Commit("base deletes name");

        var result = fixture.Event(root, established, feature, prospective: true);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("resurrects reserved basename", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void CiPullRequestUsesExplicitHeadAndProspectiveBase()
    {
        using var fixture = GitFixture.WithInitialReplay("baseline", 1);
        var root = fixture.Head;
        fixture.Git("switch", "-c", "feature");
        fixture.WriteText("feature.txt", "feature\n");
        var feature = fixture.Commit("feature");
        fixture.Git("switch", "main");
        fixture.WriteReplay("base-only", 2);
        var baseHead = fixture.Commit("base advanced");
        fixture.CreateRemoteAndPushAll();
        var synthetic = fixture.Git("commit-tree", fixture.Git("rev-parse", "HEAD^{tree}").Trim(), "-p", baseHead, "-p", feature, "-m", "synthetic checkout").Trim();
        fixture.Git("switch", "--detach", synthetic);
        var eventPath = fixture.WriteEvent(new
        {
            pull_request = new { @base = new { sha = baseHead }, head = new { sha = feature } },
        });

        var result = fixture.CiEvent("pull_request", eventPath, root);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains($"candidate={feature}", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void CiRejectsPullRequestWithUnrelatedHistory()
    {
        using var fixture = GitFixture.WithInitialReplay("baseline", 1);
        var root = fixture.Head;
        fixture.CreateRemoteAndPushAll();
        fixture.Git("switch", "--orphan", "unrelated");
        fixture.WriteEmptyLedger();
        fixture.WriteReplay("unrelated", 2);
        var head = fixture.Commit("unrelated root");
        fixture.Git("push", "origin", "unrelated");
        var eventPath = fixture.WriteEvent(new
        {
            pull_request = new { @base = new { sha = root }, head = new { sha = head } },
        });

        var result = fixture.CiEvent("pull_request", eventPath, root);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("No unique merge base", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void CiNewBranchProspectiveMergePreservesAdvancedBaseGolden()
    {
        using var fixture = GitFixture.WithInitialReplay("baseline", 1);
        var root = fixture.Head;
        fixture.Git("switch", "-c", "feature");
        fixture.WriteText("feature.txt", "feature\n");
        var feature = fixture.Commit("feature");
        fixture.Git("switch", "main");
        fixture.WriteReplay("base-only", 2);
        fixture.Commit("base adds golden");
        fixture.CreateRemoteAndPushAll();
        var eventPath = fixture.WriteEvent(new
        {
            deleted = false,
            before = new string('0', 40),
            after = feature,
            @ref = "refs/heads/feature",
            repository = new { default_branch = "main" },
        });

        var result = fixture.CiEvent("push", eventPath, root);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains($"candidate={feature}", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void CiRejectsNonFastForwardBranchUpdate()
    {
        using var fixture = GitFixture.WithInitialReplay("baseline", 1);
        var root = fixture.Head;
        fixture.WriteText("old.txt", "old\n");
        var before = fixture.Commit("old main");
        fixture.Git("branch", "old-tip", before);
        fixture.Git("switch", "--detach", root);
        fixture.WriteText("new.txt", "new\n");
        var after = fixture.Commit("replacement main");
        fixture.Git("branch", "-f", "main", after);
        fixture.Git("switch", "main");
        fixture.CreateRemoteAndPushAll();
        var eventPath = fixture.WriteEvent(new
        {
            deleted = false,
            before,
            after,
            @ref = "refs/heads/main",
            repository = new { default_branch = "main" },
        });

        var result = fixture.CiEvent("push", eventPath, root);

        Assert.NotEqual(0, result.ExitCode);
        Assert.True(result.Output.Contains("Non-fast-forward", StringComparison.Ordinal), result.Output);
    }

    [Fact]
    public void CiVerifiesNewCommitTagAndRejectsInPlaceUpdate()
    {
        using var fixture = GitFixture.WithInitialReplay("baseline", 1);
        var root = fixture.Head;
        fixture.CreateRemoteAndPushAll();
        fixture.Git("tag", "v1");
        fixture.Git("push", "origin", "refs/tags/v1");
        var target = fixture.Head;
        var createEvent = fixture.WriteEvent(new
        {
            deleted = false,
            before = new string('0', 40),
            after = target,
            @ref = "refs/tags/v1",
            repository = new { default_branch = "main" },
        });

        var created = fixture.CiEvent("push", createEvent, root);

        Assert.True(created.ExitCode == 0, created.Output);
        var updateEvent = fixture.WriteEvent(new
        {
            deleted = false,
            before = target,
            after = target,
            @ref = "refs/tags/v1",
            repository = new { default_branch = "main" },
        });
        var updated = fixture.CiEvent("push", updateEvent, root);
        Assert.NotEqual(0, updated.ExitCode);
        Assert.Contains("In-place tag updates", updated.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void CiTagRangeChainsGoldenAdditionAndReplacementFromTrustedRoot()
    {
        using var fixture = GitFixture.WithInitialReplay("baseline", 1);
        var root = fixture.Head;
        fixture.WriteReplay("tag-chain", 2);
        fixture.Commit("add tagged golden");
        var oldHash = fixture.ReplayHash("tag-chain");
        fixture.WriteReplay("tag-chain", 3);
        var newHash = fixture.ReplayHash("tag-chain");
        fixture.AppendLedger("tag-chain", oldHash, newHash, "tagged replacement");
        var target = fixture.Commit("replace tagged golden");
        fixture.CreateRemoteAndPushAll();
        fixture.Git("tag", "chain-release", target);
        fixture.Git("push", "origin", "refs/tags/chain-release");
        var eventPath = fixture.WriteEvent(new
        {
            deleted = false,
            before = new string('0', 40),
            after = target,
            @ref = "refs/tags/chain-release",
            repository = new { default_branch = "main" },
        });

        var result = fixture.CiEvent("push", eventPath, root);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains($"absent {oldHash}", result.Output, StringComparison.Ordinal);
        Assert.Contains($"{oldHash} {newHash}", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void CiPeelsAnnotatedTagsAndSkipsDeletionAndNonCommitTargets()
    {
        using var fixture = GitFixture.WithInitialReplay("baseline", 1);
        var root = fixture.Head;
        fixture.CreateRemoteAndPushAll();
        fixture.Git("tag", "-a", "annotated", "-m", "annotated replay release");
        fixture.Git("push", "origin", "refs/tags/annotated");
        var tagObject = fixture.Git("rev-parse", "refs/tags/annotated").Trim();
        var annotatedEvent = fixture.WriteEvent(new
        {
            deleted = false,
            before = new string('0', 40),
            after = tagObject,
            @ref = "refs/tags/annotated",
            repository = new { default_branch = "main" },
        });

        var annotated = fixture.CiEvent("push", annotatedEvent, root);

        Assert.True(annotated.ExitCode == 0, annotated.Output);
        fixture.WriteText("tag-payload.txt", "not a commit\n");
        var blob = fixture.Git("hash-object", "-w", "tag-payload.txt").Trim();
        fixture.Git("update-ref", "refs/tags/blob", blob);
        fixture.Git("push", "origin", "refs/tags/blob");
        var blobEvent = fixture.WriteEvent(new
        {
            deleted = false,
            before = new string('0', 40),
            after = blob,
            @ref = "refs/tags/blob",
            repository = new { default_branch = "main" },
        });
        var nonCommit = fixture.CiEvent("push", blobEvent, root);
        Assert.True(nonCommit.ExitCode == 0, nonCommit.Output);
        Assert.Contains("does not peel to a commit", nonCommit.Output, StringComparison.Ordinal);

        var deletedEvent = fixture.WriteEvent(new
        {
            deleted = true,
            before = tagObject,
            after = new string('0', 40),
            @ref = "refs/tags/annotated",
            repository = new { default_branch = "main" },
        });
        var deleted = fixture.CiEvent("push", deletedEvent, root);
        Assert.True(deleted.ExitCode == 0, deleted.Output);
        Assert.Contains("deleted ref", deleted.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void CiRejectsTagOnlyCommitAndVerifiesDeletedTagRecreation()
    {
        using var fixture = GitFixture.WithInitialReplay("baseline", 1);
        var root = fixture.Head;
        fixture.CreateRemoteAndPushAll();
        fixture.Git("switch", "-c", "tag-only");
        fixture.WriteReplay("baseline", 2);
        var tagOnly = fixture.Commit("unledgered tag-only replay change");
        fixture.Git("tag", "release", tagOnly);
        fixture.Git("push", "origin", "refs/tags/release");
        var tagOnlyEvent = fixture.WriteEvent(new
        {
            deleted = false,
            before = new string('0', 40),
            after = tagOnly,
            @ref = "refs/tags/release",
            repository = new { default_branch = "main" },
        });

        var rejected = fixture.CiEvent("push", tagOnlyEvent, root);

        Assert.NotEqual(0, rejected.ExitCode);
        Assert.Contains("not contained in default-branch history", rejected.Output, StringComparison.Ordinal);
        fixture.Git("push", "origin", ":refs/tags/release");
        var deletedEvent = fixture.WriteEvent(new
        {
            deleted = true,
            before = tagOnly,
            after = new string('0', 40),
            @ref = "refs/tags/release",
            repository = new { default_branch = "main" },
        });
        var deleted = fixture.CiEvent("push", deletedEvent, root);
        Assert.True(deleted.ExitCode == 0, deleted.Output);

        fixture.Git("switch", "main");
        fixture.Git("tag", "-f", "release", root);
        fixture.Git("push", "origin", "refs/tags/release");
        var recreatedEvent = fixture.WriteEvent(new
        {
            deleted = false,
            before = new string('0', 40),
            after = root,
            @ref = "refs/tags/release",
            repository = new { default_branch = "main" },
        });
        var recreated = fixture.CiEvent("push", recreatedEvent, root);
        Assert.True(recreated.ExitCode == 0, recreated.Output);
    }

    [Fact]
    public void CiRejectsUnrelatedNewBranchEvenAfterEstablishedRefsWereDeleted()
    {
        using var fixture = GitFixture.WithInitialReplay("baseline", 1);
        var root = fixture.Head;
        fixture.CreateRemoteAndPushAll();
        fixture.Git("switch", "--orphan", "orphan");
        fixture.WriteEmptyLedger();
        fixture.WriteReplay("orphan-golden", 2);
        var orphan = fixture.Commit("orphan root");
        fixture.Git("push", "origin", "orphan");
        var eventPath = fixture.WriteEvent(new
        {
            deleted = false,
            before = new string('0', 40),
            after = orphan,
            @ref = "refs/heads/orphan",
            repository = new { default_branch = "main" },
        });

        var result = fixture.CiEvent("push", eventPath, root);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("No unique merge base", result.Output, StringComparison.Ordinal);
    }

    private static string FindRepository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tools", "checks", "check-golden-history.ps1")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not find repository root for golden history tests.");
    }

    private sealed class GitFixture : IDisposable
    {
        public GitFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "rounds-git-fixture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Git("init", "-b", "main");
            Git("config", "user.name", "Replay Tests");
            Git("config", "user.email", "replay-tests@example.invalid");
        }

        private GitFixture(string root)
        {
            Root = root;
        }

        public string Root { get; }

        private string? RemoteRoot { get; set; }

        public string Head => Git("rev-parse", "HEAD").Trim();

        public static GitFixture WithInitialReplay(string id, ulong seed)
        {
            var fixture = new GitFixture();
            fixture.WriteEmptyLedger();
            fixture.WriteReplay(id, seed);
            fixture.Commit("root");
            return fixture;
        }

        public static GitFixture CloneShallow(string source)
        {
            var root = Path.Combine(Path.GetTempPath(), "rounds-git-fixture-" + Guid.NewGuid().ToString("N"));
            var result = RunProcess(Path.GetTempPath(), "git", "clone", "--depth", "1", new Uri(source).AbsoluteUri, root);
            Assert.Equal(0, result.ExitCode);
            return new GitFixture(root);
        }

        public string Git(params string[] arguments)
        {
            var result = RunProcess(Root, "git", arguments);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {result.Output}");
            }
            return result.Output;
        }

        public string Commit(string message)
        {
            Git("add", ".");
            Git("commit", "-m", message);
            return Head;
        }

        public void WriteEmptyLedger() => WriteText("replays/intentional-breaks.md", "# Intentional replay breaks\n");

        public void AppendLedger(string id, string oldHash, string newHash, string reason)
        {
            var path = Path.Combine(Root, "replays", "intentional-breaks.md");
            var existing = File.ReadAllText(path);
            var separator = existing == "# Intentional replay breaks\n" ? "\n" : string.Empty;
            File.AppendAllText(path, $"{separator}- replay: {id}{ReplayFormat.FileSuffix}; old: {oldHash}; new: {newHash}; reason: {reason}\n");
        }

        public void WriteReplay(string id, ulong seed)
        {
            var recorder = new ReplayRecorder(id, seed, "arena-006", 1);
            recorder.Step(
            [
                new PlayerInput(0, false, false, false, new Vec2(1, 0)),
                new PlayerInput(0, false, false, false, new Vec2(-1, 0)),
            ]);
            var path = Path.Combine(Root, "replays", "golden", id + ReplayFormat.FileSuffix);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = File.Create(path);
            ReplayCodec.Write(stream, recorder.Finish());
        }

        public string ReplayHash(string id)
        {
            using var stream = File.OpenRead(Path.Combine(Root, "replays", "golden", id + ReplayFormat.FileSuffix));
            return ReplayCodec.Load(stream).FinalHash.ToString("x16");
        }

        public void DeleteReplay(string id) => File.Delete(Path.Combine(Root, "replays", "golden", id + ReplayFormat.FileSuffix));

        public void WriteText(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public ProcessResult PowerShell(string script, params string[] arguments) =>
            RunProcess(Root, "pwsh", ["-NoProfile", "-File", script, .. arguments]);

        public ProcessResult Event(string historyBase, string established, string candidate, bool prospective = false)
        {
            var arguments = new List<string>
            {
                "-NoProfile", "-File", EventScript,
                "-HistoryBase", historyBase,
                "-Established", established,
                "-Candidate", candidate,
                "-Repository", Root,
                "-TrustedRoot", Git("rev-list", "--max-parents=0", "HEAD").Trim(),
            };
            if (prospective) { arguments.Add("-ProspectiveMerge"); }
            return RunProcess(
                Root,
                "pwsh",
                arguments,
                new Dictionary<string, string>
                {
                    ["ROUNDS_DOTNET"] = Path.Combine(Repository, ".tools", "dotnet", "dotnet.exe"),
                    ["ROUNDS_HARNESS_PROJECT"] = Path.Combine(Repository, "src", "Rounds.Harness", "Rounds.Harness.csproj"),
                });
        }

        public void CreateRemoteAndPushAll()
        {
            RemoteRoot = Root + ".remote.git";
            var initialized = RunProcess(Path.GetTempPath(), "git", "init", "--bare", RemoteRoot);
            if (initialized.ExitCode != 0) { throw new InvalidOperationException(initialized.Output); }
            Git("remote", "add", "origin", RemoteRoot);
            Git("push", "--all", "origin");
            Git("push", "--tags", "origin");
        }

        public string WriteEvent(object value)
        {
            var path = Path.Combine(Root, "event.json");
            File.WriteAllText(path, JsonSerializer.Serialize(value));
            return path;
        }

        public ProcessResult CiEvent(string eventName, string eventPath, string trustedRoot) =>
            RunProcess(
                Root,
                "pwsh",
                ["-NoProfile", "-File", CiEventScript, "-EventName", eventName, "-EventPath", eventPath, "-TrustedRoot", trustedRoot, "-Repository", Root],
                new Dictionary<string, string>
                {
                    ["ROUNDS_DOTNET"] = Path.Combine(Repository, ".tools", "dotnet", "dotnet.exe"),
                    ["ROUNDS_HARNESS_PROJECT"] = Path.Combine(Repository, "src", "Rounds.Harness", "Rounds.Harness.csproj"),
                });

        public void Dispose()
        {
            if (!Directory.Exists(Root)) { return; }
            foreach (var path in Directory.EnumerateFileSystemEntries(Root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }
            Directory.Delete(Root, recursive: true);
            if (RemoteRoot is not null && Directory.Exists(RemoteRoot))
            {
                foreach (var path in Directory.EnumerateFileSystemEntries(RemoteRoot, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                }
                Directory.Delete(RemoteRoot, recursive: true);
            }
        }
    }

    private static ProcessResult RunProcess(string workingDirectory, string executable, params string[] arguments) =>
        RunProcess(workingDirectory, executable, arguments, null);

    private static ProcessResult RunProcess(
        string workingDirectory,
        string executable,
        IEnumerable<string> arguments,
        IReadOnlyDictionary<string, string>? environment)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments) { start.ArgumentList.Add(argument); }
        if (environment is not null)
        {
            foreach (var pair in environment) { start.Environment[pair.Key] = pair.Value; }
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {executable}.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, output + error);
    }

    private sealed record ProcessResult(int ExitCode, string Output);
}
