using System;
using System.Collections.Generic;

public enum MissilePoolReservationFailure
{
    None,
    InvalidMissileCount,
    MissileCountExceedsConfiguredMaximum,
    PoolCapacityUnavailable,
}

public sealed class MissilePoolReservation
{
    internal MissilePoolReservation(
        MissilePoolLedger owner,
        long reservationId,
        int requestedCount)
    {
        Owner = owner;
        ReservationId = reservationId;
        RequestedCount = requestedCount;
        RemainingCount = requestedCount;
        IsOpen = true;
    }

    internal MissilePoolLedger Owner { get; }

    public long ReservationId { get; }
    public int RequestedCount { get; }
    public int RemainingCount { get; internal set; }
    public int LeasedCount { get; internal set; }
    public bool IsOpen { get; internal set; }
}

public sealed class MissilePoolLease
{
    internal MissilePoolLease(
        MissilePoolLedger owner,
        long leaseId,
        MissilePoolReservation reservation,
        int slotId)
    {
        Owner = owner;
        LeaseId = leaseId;
        ReservationId = reservation.ReservationId;
        SlotId = slotId;
    }

    internal MissilePoolLedger Owner { get; }

    public long LeaseId { get; }
    public long ReservationId { get; }
    public int SlotId { get; }
    public bool IsReturned { get; internal set; }
}

/// <summary>
/// Tracks the ownership state of a fixed set of missile slots.
/// This class deliberately has no UnityEngine dependency so reservation rules can be
/// validated independently from GameObject lifetime and frame timing.
/// </summary>
public sealed class MissilePoolLedger
{
    public const int DefaultCapacity = 40;
    public const int DefaultMaximumReservationSize = 30;

    private sealed class ReservationRecord
    {
        public ReservationRecord(MissilePoolReservation token, Queue<int> reservedSlotIds)
        {
            Token = token;
            ReservedSlotIds = reservedSlotIds;
        }

        public MissilePoolReservation Token { get; }
        public Queue<int> ReservedSlotIds { get; }
        public int OutstandingLeaseCount { get; set; }
    }

    private sealed class LeaseRecord
    {
        public LeaseRecord(
            MissilePoolLease token,
            ReservationRecord reservation,
            int slotId)
        {
            Token = token;
            Reservation = reservation;
            SlotId = slotId;
        }

        public MissilePoolLease Token { get; }
        public ReservationRecord Reservation { get; }
        public int SlotId { get; }
    }

    private readonly Queue<int> availableSlotIds;
    private readonly Dictionary<long, ReservationRecord> reservations = new();
    private readonly Dictionary<long, LeaseRecord> leases = new();
    private long nextReservationId = 1;
    private long nextLeaseId = 1;
    private int reservedCount;

    public MissilePoolLedger(
        int capacity = DefaultCapacity,
        int maximumReservationSize = DefaultMaximumReservationSize)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        }

        if (maximumReservationSize <= 0 || maximumReservationSize > capacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumReservationSize),
                "Maximum reservation size must be between one and the pool capacity.");
        }

        Capacity = capacity;
        MaximumReservationSize = maximumReservationSize;
        availableSlotIds = new Queue<int>(capacity);
        for (int slotId = 0; slotId < capacity; slotId++)
        {
            availableSlotIds.Enqueue(slotId);
        }
    }

    public int Capacity { get; }
    public int MaximumReservationSize { get; }
    public int TotalCount => Capacity;
    public int AvailableCount => availableSlotIds.Count;
    public int ReservedCount => reservedCount;
    public int LeasedCount => leases.Count;

    public bool HasValidCounts =>
        AvailableCount >= 0 &&
        ReservedCount >= 0 &&
        LeasedCount >= 0 &&
        AvailableCount + ReservedCount + LeasedCount == TotalCount;

    /// <summary>
    /// Atomically removes <paramref name="missileCount"/> slots from the available set.
    /// A failed reservation never changes counts or consumes an identifier.
    /// </summary>
    public bool TryReserve(
        int missileCount,
        out MissilePoolReservation reservation,
        out MissilePoolReservationFailure failure)
    {
        reservation = null;

        if (missileCount <= 0)
        {
            failure = MissilePoolReservationFailure.InvalidMissileCount;
            return false;
        }

        if (missileCount > MaximumReservationSize)
        {
            failure = MissilePoolReservationFailure.MissileCountExceedsConfiguredMaximum;
            return false;
        }

        if (missileCount > availableSlotIds.Count)
        {
            failure = MissilePoolReservationFailure.PoolCapacityUnavailable;
            return false;
        }

        Queue<int> reservedSlotIds = new(missileCount);
        for (int i = 0; i < missileCount; i++)
        {
            reservedSlotIds.Enqueue(availableSlotIds.Dequeue());
        }

        reservation = new MissilePoolReservation(this, nextReservationId++, missileCount);
        reservations.Add(
            reservation.ReservationId,
            new ReservationRecord(reservation, reservedSlotIds));
        reservedCount += missileCount;
        failure = MissilePoolReservationFailure.None;
        return true;
    }

    /// <summary>
    /// Converts one reserved slot into a live lease. When the last reserved slot is
    /// leased, the reservation is automatically closed to further leasing.
    /// </summary>
    public bool TryLeaseReserved(
        MissilePoolReservation reservation,
        out MissilePoolLease lease)
    {
        lease = null;
        if (!TryGetOpenReservation(reservation, out ReservationRecord record) ||
            record.ReservedSlotIds.Count == 0)
        {
            return false;
        }

        int slotId = record.ReservedSlotIds.Dequeue();
        reservedCount--;
        record.OutstandingLeaseCount++;
        reservation.RemainingCount--;
        reservation.LeasedCount++;

        if (record.ReservedSlotIds.Count == 0)
        {
            reservation.IsOpen = false;
        }

        lease = new MissilePoolLease(this, nextLeaseId++, reservation, slotId);
        leases.Add(lease.LeaseId, new LeaseRecord(lease, record, slotId));
        return true;
    }

    /// <summary>
    /// Returns every slot that has not yet been leased and closes the reservation.
    /// Returns false for an already closed, stale, or foreign reservation.
    /// </summary>
    public bool ReleaseUnusedReservation(
        MissilePoolReservation reservation,
        out int releasedCount)
    {
        releasedCount = 0;
        if (!TryGetOpenReservation(reservation, out ReservationRecord record))
        {
            return false;
        }

        reservation.IsOpen = false;
        while (record.ReservedSlotIds.Count > 0)
        {
            availableSlotIds.Enqueue(record.ReservedSlotIds.Dequeue());
            releasedCount++;
        }

        reservedCount -= releasedCount;
        reservation.RemainingCount = 0;
        RemoveCompletedReservation(record);
        return true;
    }

    /// <summary>
    /// Returns one live lease to the available set. Duplicate, stale, or foreign lease
    /// tokens are rejected without changing any state.
    /// </summary>
    public bool ReturnLeased(MissilePoolLease lease)
    {
        if (lease == null ||
            !ReferenceEquals(lease.Owner, this) ||
            !leases.TryGetValue(lease.LeaseId, out LeaseRecord record) ||
            !ReferenceEquals(record.Token, lease) ||
            record.SlotId != lease.SlotId)
        {
            return false;
        }

        leases.Remove(lease.LeaseId);
        availableSlotIds.Enqueue(record.SlotId);
        lease.IsReturned = true;

        record.Reservation.OutstandingLeaseCount--;
        record.Reservation.Token.LeasedCount--;
        RemoveCompletedReservation(record.Reservation);
        return true;
    }

    private bool TryGetOpenReservation(
        MissilePoolReservation reservation,
        out ReservationRecord record)
    {
        record = null;
        return reservation != null &&
               ReferenceEquals(reservation.Owner, this) &&
               reservation.IsOpen &&
               reservations.TryGetValue(reservation.ReservationId, out record) &&
               ReferenceEquals(record.Token, reservation);
    }

    private void RemoveCompletedReservation(ReservationRecord record)
    {
        if (!record.Token.IsOpen &&
            record.ReservedSlotIds.Count == 0 &&
            record.OutstandingLeaseCount == 0)
        {
            reservations.Remove(record.Token.ReservationId);
        }
    }
}
