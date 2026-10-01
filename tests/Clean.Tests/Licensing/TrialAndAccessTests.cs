using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Clean.Core.Interfaces;
using Clean.Core.Licensing;
using Clean.Core.Models;
using Clean.Infrastructure.Licensing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clean.Tests.Licensing;

public sealed class TrialAndAccessTests : IDisposable
{
    private const string Device = "device-a";

    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly TestDirectory _folder = new();
    private readonly ECDsa _serverKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly Clock _clock = new(Start);

    public void Dispose()
    {
        _folder.Dispose();
        _serverKey.Dispose();
    }

    [Theory]
    [InlineData(0, AccessMode.Trial, 5)]
    [InlineData(1, AccessMode.Trial, 4)]
    [InlineData(4.5, AccessMode.Trial, 1)]
    [InlineData(5, AccessMode.TrialEnded, 0)]
    [InlineData(30, AccessMode.TrialEnded, 0)]
    public void Evaluate_FollowsTheFiveDayTrial(double daysAfterStart, AccessMode expected, int daysLeft)
    {
        var access = LicenseEvaluator.Evaluate(null, new TrialRecord(Start, Start), Start.AddDays(daysAfterStart));

        Assert.Equal(expected, access.Mode);
        Assert.Equal(daysLeft, access.DaysLeft);
        Assert.Equal(expected != AccessMode.TrialEnded, access.CanClean);
    }

    [Fact]
    public void Evaluate_AValidLicenceUnlocksEverythingEvenAfterTheTrial()
    {
        var licence = new LicenseCheck(LicenseState.Valid, null, "ok");

        var access = LicenseEvaluator.Evaluate(licence, new TrialRecord(Start, Start), Start.AddDays(400));

        Assert.True(access.IsLicensed);
        Assert.True(access.CanClean);
    }

    [Fact]
    public void Evaluate_TheOwnerPlanIsFullAccessWithItsOwnTitle()
    {
        var token = new LicenseToken(1, "OWNER", LicensePlans.Owner, Start, Start.AddYears(70), null);

        var access = LicenseEvaluator.Evaluate(new LicenseCheck(LicenseState.Valid, token, "ok"), new TrialRecord(Start, Start), Start.AddDays(900));

        Assert.True(access.IsLicensed);
        Assert.True(access.CanClean);
        Assert.Equal("Compte propriétaire", access.Title);
    }

    [Fact]
    public void Service_AnOwnerTokenWithoutADeviceWorksOnAnyPc()
    {
        var service = Service();
        _clock.Now = Start.AddDays(10);

        var check = service.Activate(Sign(new LicenseToken(1, "OWNER", LicensePlans.Owner, Start.AddDays(-1), Start.AddYears(70), null)));

        Assert.True(check.AllowsPaidFeatures);
        Assert.Equal("Compte propriétaire", service.Access.Title);
    }

    [Fact]
    public void Evaluate_AnInvalidOrExpiredLicenceFallsBackToTheTrial()
    {
        var expired = new LicenseCheck(LicenseState.Expired, null, "expired");

        Assert.Equal(AccessMode.TrialEnded, LicenseEvaluator.Evaluate(expired, new TrialRecord(Start, Start), Start.AddDays(30)).Mode);
        Assert.Equal(AccessMode.Trial, LicenseEvaluator.Evaluate(expired, new TrialRecord(Start, Start), Start.AddDays(1)).Mode);
    }

    [Fact]
    public void Evaluate_EndsTheTrialWhenTheClockWasPutBackByDays()
    {
        var trial = new TrialRecord(Start, Start.AddDays(4));

        var access = LicenseEvaluator.Evaluate(null, trial, Start.AddDays(1));

        Assert.Equal(AccessMode.TrialEnded, access.Mode);
        Assert.Contains("reculée", access.Detail);
    }

    [Fact]
    public void Evaluate_ToleratesASmallClockCorrection()
    {
        var trial = new TrialRecord(Start, Start.AddDays(1));

        Assert.Equal(AccessMode.Trial, LicenseEvaluator.Evaluate(null, trial, Start.AddHours(1)).Mode);
    }

    [Fact]
    public void TrialStore_StartsOnFirstLaunchAndKeepsTheStartDate()
    {
        var store = new TrialStore(Path.Combine(_folder.RootPath, "trial.json"));

        var first = store.LoadOrStart(Start);
        var later = store.LoadOrStart(Start.AddDays(3));

        Assert.Equal(Start, first.StartedAt);
        Assert.Equal(Start, later.StartedAt);
        Assert.Equal(Start.AddDays(3), later.LastSeenAt);
    }

    [Fact]
    public void TrialStore_NeverMovesLastSeenBackwards()
    {
        var store = new TrialStore(Path.Combine(_folder.RootPath, "trial.json"));
        store.LoadOrStart(Start);
        store.LoadOrStart(Start.AddDays(3));

        var rolledBack = store.LoadOrStart(Start.AddDays(1));

        Assert.Equal(Start.AddDays(3), rolledBack.LastSeenAt);
    }

    [Fact]
    public void TrialStore_SurvivesAnUnreadableFile()
    {
        var path = Path.Combine(_folder.RootPath, "trial.json");
        File.WriteAllText(path, "{ not json");

        var record = new TrialStore(path).LoadOrStart(Start);

        Assert.Equal(Start, record.StartedAt);
    }

    [Fact]
    public void Service_StartsInTrialAndEndsItWhenTheTimeComes()
    {
        var service = Service();

        Assert.Equal(AccessMode.Trial, service.Access.Mode);

        _clock.Now = Start.AddDays(6);

        Assert.Equal(AccessMode.TrialEnded, service.Access.Mode);
        Assert.False(service.Access.CanClean);
    }

    [Fact]
    public void Service_ActivatesAValidLicenceAndRemembersItAfterARestart()
    {
        var service = Service();
        _clock.Now = Start.AddDays(10);
        var changed = 0;
        service.Changed += () => changed++;

        var check = service.Activate(Sign(Token()));

        Assert.True(check.AllowsPaidFeatures);
        Assert.True(service.Access.IsLicensed);
        Assert.Equal(1, changed);
        Assert.True(Service().Access.IsLicensed);
    }

    [Fact]
    public void Service_RefusesAndDoesNotStoreALicenceForAnotherPc()
    {
        var service = Service();

        var check = service.Activate(Sign(Token() with { DeviceId = "someone-else" }));

        Assert.False(check.AllowsPaidFeatures);
        Assert.False(service.Access.IsLicensed);
        Assert.Null(new LicenseStore(Path.Combine(_folder.RootPath, "license.key")).Load());
    }

    [Fact]
    public void Service_RefusesEverythingWhenNoPublicKeyIsEmbedded()
    {
        var service = Service(withKey: false);

        var check = service.Activate(Sign(Token()));

        Assert.False(service.CanVerifyLicenses);
        Assert.Equal(LicenseState.Invalid, check.State);
        Assert.Equal(AccessMode.Trial, service.Access.Mode);
    }

    [Fact]
    public void Service_DeactivateGoesBackToTheTrialState()
    {
        var service = Service();
        _clock.Now = Start.AddDays(10);
        service.Activate(Sign(Token()));

        service.Deactivate();

        Assert.Equal(AccessMode.TrialEnded, service.Access.Mode);
    }

    [Fact]
    public async Task Cleaners_AreRefusedAfterTheTrialAndAllowedWithALicence()
    {
        var service = Service();
        _clock.Now = Start.AddDays(10);
        var cleaner = new LicensedCleaner(new RecordingCleaner(), service);
        var remover = new LicensedFileRemover(new RecordingRemover(), service);

        await Assert.ThrowsAsync<LicenseRequiredException>(() => cleaner.CleanAsync([], null, CancellationToken.None));
        await Assert.ThrowsAsync<LicenseRequiredException>(() => remover.RemoveAsync([], "X", "X", null, CancellationToken.None));

        service.Activate(Sign(Token()));

        Assert.NotNull(await cleaner.CleanAsync([], null, CancellationToken.None));
        Assert.NotNull(await remover.RemoveAsync([], "X", "X", null, CancellationToken.None));
    }

    [Fact]
    public async Task Cleaners_WorkDuringTheTrial()
    {
        var service = Service();

        Assert.NotNull(await new LicensedCleaner(new RecordingCleaner(), service).CleanAsync([], null, CancellationToken.None));
    }

    private LicenseService Service(bool withKey = true) => new(
        new LicenseStore(Path.Combine(_folder.RootPath, "license.key")),
        new TrialStore(Path.Combine(_folder.RootPath, "trial.json")),
        withKey ? new LicenseVerifier(_serverKey.ExportSubjectPublicKeyInfo()) : null,
        Device,
        _clock,
        NullLogger<LicenseService>.Instance);

    private LicenseToken Token() => new(1, "LIC-1", "lifetime", Start.AddDays(-1), Start.AddYears(70), Device);

    private string Sign(LicenseToken token)
    {
        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(token, Json));
        var signature = _serverKey.SignData(payload, HashAlgorithmName.SHA256);
        return $"{Encode(payload)}.{Encode(signature)}";
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingCleaner : ICleaner
    {
        public Task<CleaningResult> CleanAsync(IReadOnlyList<CleaningDecision> decisions, IProgress<CleaningProgress>? progress, CancellationToken cancellationToken) =>
            Task.FromResult(new CleaningResult(0, 0, 0, 0, 0, false, TimeSpan.Zero, null));
    }

    private sealed class RecordingRemover : IFileRemover
    {
        public Task<CleaningResult> RemoveAsync(IReadOnlyList<RemovalRequest> requests, string ruleId, string label, IProgress<CleaningProgress>? progress, CancellationToken cancellationToken) =>
            Task.FromResult(new CleaningResult(0, 0, 0, 0, 0, false, TimeSpan.Zero, null));
    }
}
