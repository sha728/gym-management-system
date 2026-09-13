namespace GymApi.Models;

// This is the admin's stored decision. Active and Expired are calculated from EndDate.
public enum MembershipStatus
{
    Pending,
    Approved,
    Rejected
}
