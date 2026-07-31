using System.Collections.Generic;
using NUnit.Framework;

public class MissilePoolLedgerTests
{
    [Test]
    public void DefaultLedger_StartsWithExactlyFortyAvailableSlots()
    {
        MissilePoolLedger ledger = new();

        AssertCounts(ledger, available: 40, reserved: 0, leased: 0);
        Assert.That(ledger.MaximumReservationSize, Is.EqualTo(30));
    }

    [TestCase(5)]
    [TestCase(10)]
    [TestCase(15)]
    [TestCase(20)]
    [TestCase(30)]
    public void SupportedReservationSizes_LeaseAndReturnEverySlot(int missileCount)
    {
        MissilePoolLedger ledger = new();

        Assert.That(
            ledger.TryReserve(missileCount, out MissilePoolReservation reservation, out MissilePoolReservationFailure failure),
            Is.True);
        Assert.That(failure, Is.EqualTo(MissilePoolReservationFailure.None));
        AssertCounts(ledger, 40 - missileCount, missileCount, 0);

        List<MissilePoolLease> leases = LeaseAll(ledger, reservation);

        Assert.That(leases, Has.Count.EqualTo(missileCount));
        Assert.That(reservation.IsOpen, Is.False);
        Assert.That(reservation.RemainingCount, Is.Zero);
        Assert.That(reservation.LeasedCount, Is.EqualTo(missileCount));
        AssertCounts(ledger, 40 - missileCount, 0, missileCount);

        foreach (MissilePoolLease lease in leases)
        {
            Assert.That(ledger.ReturnLeased(lease), Is.True);
        }

        AssertCounts(ledger, 40, 0, 0);
    }

    [Test]
    public void ReservationAboveConfiguredMaximum_IsRejectedWithoutMutation()
    {
        MissilePoolLedger ledger = new();

        bool reserved = ledger.TryReserve(
            31,
            out MissilePoolReservation reservation,
            out MissilePoolReservationFailure failure);

        Assert.That(reserved, Is.False);
        Assert.That(reservation, Is.Null);
        Assert.That(failure, Is.EqualTo(MissilePoolReservationFailure.MissileCountExceedsConfiguredMaximum));
        AssertCounts(ledger, 40, 0, 0);
    }

    [Test]
    public void FailedReservation_DoesNotConsumeReservationIdentifier()
    {
        MissilePoolLedger ledger = new();

        Assert.That(ledger.TryReserve(31, out _, out _), Is.False);
        Assert.That(ledger.TryReserve(1, out MissilePoolReservation reservation, out _), Is.True);

        Assert.That(reservation.ReservationId, Is.EqualTo(1));
        Assert.That(ledger.ReleaseUnusedReservation(reservation, out _), Is.True);
        AssertCounts(ledger, 40, 0, 0);
    }

    [Test]
    public void InsufficientCapacity_IsRejectedAtomically()
    {
        MissilePoolLedger ledger = new();
        Assert.That(ledger.TryReserve(30, out _, out _), Is.True);
        AssertCounts(ledger, 10, 30, 0);

        bool reserved = ledger.TryReserve(
            11,
            out MissilePoolReservation reservation,
            out MissilePoolReservationFailure failure);

        Assert.That(reserved, Is.False);
        Assert.That(reservation, Is.Null);
        Assert.That(failure, Is.EqualTo(MissilePoolReservationFailure.PoolCapacityUnavailable));
        AssertCounts(ledger, 10, 30, 0);
    }

    [Test]
    public void SeparateReservations_CanUseAllFortySlotsWithoutExpansion()
    {
        MissilePoolLedger ledger = new();
        Assert.That(ledger.TryReserve(30, out MissilePoolReservation first, out _), Is.True);
        Assert.That(ledger.TryReserve(10, out MissilePoolReservation second, out _), Is.True);
        Assert.That(ledger.TryReserve(1, out _, out MissilePoolReservationFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(MissilePoolReservationFailure.PoolCapacityUnavailable));
        AssertCounts(ledger, 0, 40, 0);

        List<MissilePoolLease> leases = LeaseAll(ledger, first);
        leases.AddRange(LeaseAll(ledger, second));
        AssertCounts(ledger, 0, 0, 40);

        foreach (MissilePoolLease lease in leases)
        {
            Assert.That(ledger.ReturnLeased(lease), Is.True);
        }

        AssertCounts(ledger, 40, 0, 0);
    }

    [Test]
    public void CancelPartiallyLeasedReservation_ReleasesOnlyUnusedSlotsExactlyOnce()
    {
        MissilePoolLedger ledger = new();
        Assert.That(ledger.TryReserve(5, out MissilePoolReservation reservation, out _), Is.True);
        Assert.That(ledger.TryLeaseReserved(reservation, out MissilePoolLease firstLease), Is.True);
        Assert.That(ledger.TryLeaseReserved(reservation, out MissilePoolLease secondLease), Is.True);

        Assert.That(ledger.ReleaseUnusedReservation(reservation, out int releasedCount), Is.True);
        Assert.That(releasedCount, Is.EqualTo(3));
        AssertCounts(ledger, 38, 0, 2);

        Assert.That(ledger.ReleaseUnusedReservation(reservation, out int duplicateReleaseCount), Is.False);
        Assert.That(duplicateReleaseCount, Is.Zero);
        AssertCounts(ledger, 38, 0, 2);

        Assert.That(ledger.ReturnLeased(firstLease), Is.True);
        Assert.That(ledger.ReturnLeased(secondLease), Is.True);
        AssertCounts(ledger, 40, 0, 0);
    }

    [Test]
    public void ReturnedLease_CannotBeReturnedTwice()
    {
        MissilePoolLedger ledger = new();
        Assert.That(ledger.TryReserve(1, out MissilePoolReservation reservation, out _), Is.True);
        Assert.That(ledger.TryLeaseReserved(reservation, out MissilePoolLease lease), Is.True);

        Assert.That(ledger.ReturnLeased(lease), Is.True);
        Assert.That(lease.IsReturned, Is.True);
        Assert.That(ledger.ReturnLeased(lease), Is.False);
        AssertCounts(ledger, 40, 0, 0);
    }

    [Test]
    public void FullyLeasedReservation_HasNoUnusedSlotsToRelease()
    {
        MissilePoolLedger ledger = new();
        Assert.That(ledger.TryReserve(1, out MissilePoolReservation reservation, out _), Is.True);
        Assert.That(ledger.TryLeaseReserved(reservation, out MissilePoolLease lease), Is.True);

        Assert.That(ledger.ReleaseUnusedReservation(reservation, out int releasedCount), Is.False);
        Assert.That(releasedCount, Is.Zero);
        AssertCounts(ledger, 39, 0, 1);

        Assert.That(ledger.ReturnLeased(lease), Is.True);
        AssertCounts(ledger, 40, 0, 0);
    }

    [Test]
    public void ForeignLedger_RejectsReservationAndLeaseTokens()
    {
        MissilePoolLedger owner = new();
        MissilePoolLedger foreign = new();
        Assert.That(owner.TryReserve(2, out MissilePoolReservation reservation, out _), Is.True);
        Assert.That(owner.TryLeaseReserved(reservation, out MissilePoolLease lease), Is.True);

        Assert.That(foreign.TryLeaseReserved(reservation, out _), Is.False);
        Assert.That(foreign.ReleaseUnusedReservation(reservation, out int releasedCount), Is.False);
        Assert.That(releasedCount, Is.Zero);
        Assert.That(foreign.ReturnLeased(lease), Is.False);
        AssertCounts(foreign, 40, 0, 0);
        AssertCounts(owner, 38, 1, 1);

        Assert.That(owner.ReleaseUnusedReservation(reservation, out _), Is.True);
        Assert.That(owner.ReturnLeased(lease), Is.True);
        AssertCounts(owner, 40, 0, 0);
    }

    [Test]
    public void ReleasedReservation_IsStaleAndCannotLeaseAgain()
    {
        MissilePoolLedger ledger = new();
        Assert.That(ledger.TryReserve(3, out MissilePoolReservation reservation, out _), Is.True);
        Assert.That(ledger.ReleaseUnusedReservation(reservation, out int releasedCount), Is.True);

        Assert.That(releasedCount, Is.EqualTo(3));
        Assert.That(ledger.TryLeaseReserved(reservation, out _), Is.False);
        Assert.That(ledger.ReleaseUnusedReservation(reservation, out _), Is.False);
        AssertCounts(ledger, 40, 0, 0);
    }

    [Test]
    public void RepeatedFullReservations_NeverCreateAnExtraSlotOrDuplicateSlotId()
    {
        MissilePoolLedger ledger = new();

        for (int cycle = 0; cycle < 100; cycle++)
        {
            Assert.That(ledger.TryReserve(30, out MissilePoolReservation reservation, out _), Is.True);
            List<MissilePoolLease> leases = LeaseAll(ledger, reservation);
            HashSet<int> uniqueSlotIds = new();

            foreach (MissilePoolLease lease in leases)
            {
                Assert.That(lease.SlotId, Is.InRange(0, 39));
                Assert.That(uniqueSlotIds.Add(lease.SlotId), Is.True);
                Assert.That(ledger.ReturnLeased(lease), Is.True);
            }

            Assert.That(uniqueSlotIds, Has.Count.EqualTo(30));
            AssertCounts(ledger, 40, 0, 0);
        }
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void NonPositiveReservationSize_IsRejectedWithoutMutation(int missileCount)
    {
        MissilePoolLedger ledger = new();

        Assert.That(ledger.TryReserve(missileCount, out _, out MissilePoolReservationFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(MissilePoolReservationFailure.InvalidMissileCount));
        AssertCounts(ledger, 40, 0, 0);
    }

    private static List<MissilePoolLease> LeaseAll(
        MissilePoolLedger ledger,
        MissilePoolReservation reservation)
    {
        List<MissilePoolLease> leases = new();
        while (ledger.TryLeaseReserved(reservation, out MissilePoolLease lease))
        {
            leases.Add(lease);
        }

        return leases;
    }

    private static void AssertCounts(
        MissilePoolLedger ledger,
        int available,
        int reserved,
        int leased)
    {
        Assert.That(ledger.TotalCount, Is.EqualTo(40));
        Assert.That(ledger.AvailableCount, Is.EqualTo(available));
        Assert.That(ledger.ReservedCount, Is.EqualTo(reserved));
        Assert.That(ledger.LeasedCount, Is.EqualTo(leased));
        Assert.That(ledger.HasValidCounts, Is.True);
    }
}
