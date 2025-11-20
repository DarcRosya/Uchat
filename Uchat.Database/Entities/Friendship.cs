using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Uchat.Database.Entities
{
    /// <summary>
    /// Система друзей (Friendship) — как в Discord
    /// </summary>
    public class Friendship
    {
        public int Id { get; set; }

        // Кто отправил запрос
        public int SenderId { get; set; }
        public User Sender { get; set; } = null!;

        // Кто получил запрос
        public int ReceiverId { get; set; }
        public User Receiver { get; set; } = null!;

        // Статус дружбы
        public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public enum FriendshipStatus
    {
        Pending = 0,
        Accepted = 1,
        Rejected = 2,
        Blocked = 3
    }
}
