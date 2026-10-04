using System;
using Unity.Collections;
using Unity.Netcode;

namespace MultiplayerGame.Networking
{
    /// <summary>
    /// Network-serializable struct representing a connected player's session information.
    /// </summary>
    public struct PlayerData : INetworkSerializable, IEquatable<PlayerData>
    {
        public ulong ClientId;
        public FixedString64Bytes PlayerName;
        public bool IsReady;
        public bool IsHost;

        public PlayerData(ulong clientId, string playerName, bool isReady = false, bool isHost = false)
        {
            ClientId = clientId;
            PlayerName = new FixedString64Bytes(playerName);
            IsReady = isReady;
            IsHost = isHost;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref IsReady);
            serializer.SerializeValue(ref IsHost);
        }

        public bool Equals(PlayerData other)
        {
            return ClientId == other.ClientId &&
                   PlayerName.Equals(other.PlayerName) &&
                   IsReady == other.IsReady &&
                   IsHost == other.IsHost;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ClientId, PlayerName, IsReady, IsHost);
        }

        public override string ToString()
        {
            return $"Player [{ClientId}] '{PlayerName}' (Ready: {IsReady}, Host: {IsHost})";
        }
    }
}
