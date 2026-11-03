using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;

// This local-only identity selects a ticket; it never authenticates a player.
public struct ClientConnectionEndpoint : IComponentData
{
    public FixedString512Bytes Value;
    public FixedString128Bytes Profile;
    public int ResumePlayerId;
    public bool FreshJoin;
}
public enum ResumeLookup : byte { None, Found, ProfileRequired, Ambiguous, Unavailable }

public static class PlayerResumeStore
{
    [Serializable]
    public sealed class Receipt
    {
        public string Endpoint;
        public ulong MatchSessionId;
        public string Ticket;
        public string Profile;
        public int PlayerId;
        public long SavedUtcTicks;
    }
    public static string LastError { get; private set; }
    static string DirectoryPath => Path.Combine(Application.persistentDataPath, "NetworkResume");

    public static void Configure(World client, NetworkEndpoint endpoint, bool freshJoin = false, string profile = "", int resumePlayerId = -1)
    {
        using var query = client.EntityManager.CreateEntityQuery(typeof(ClientConnectionEndpoint));
        var entity = query.CalculateEntityCount() == 0 ? client.EntityManager.CreateEntity(typeof(ClientConnectionEndpoint)) : query.GetSingletonEntity();
        client.EntityManager.SetComponentData(entity, new ClientConnectionEndpoint
        { Value = new FixedString512Bytes(endpoint.ToString()), FreshJoin = freshJoin, Profile = new FixedString128Bytes((profile ?? string.Empty).Trim()), ResumePlayerId = resumePlayerId });
    }

    public static string Endpoint(EntityManager em, Entity connection)
    {
        using var configured = em.CreateEntityQuery(typeof(ClientConnectionEndpoint));
        if (configured.CalculateEntityCount() == 1) return configured.GetSingleton<ClientConnectionEndpoint>().Value.ToString();
        using var drivers = em.CreateEntityQuery(typeof(NetworkStreamDriver));
        if (drivers.CalculateEntityCount() != 1 || !em.HasComponent<NetworkStreamConnection>(connection)) return null;
        return drivers.GetSingletonRW<NetworkStreamDriver>().ValueRO.GetRemoteEndPoint(em.GetComponentData<NetworkStreamConnection>(connection)).ToString();
    }

    static string Key(string endpoint)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(endpoint))).Replace("-", "").ToLowerInvariant();
    }

    public static ResumeLookup Lookup(string endpoint, string profile, int playerId, out Receipt receipt) => LookupAt(DirectoryPath, endpoint, profile, playerId, out receipt);
    public static bool Save(string endpoint, ulong match, FixedString128Bytes ticket, int playerId, string profile) => SaveAt(DirectoryPath, endpoint, match, ticket, playerId, profile);

    // The explicit directory variants allow checks to use a disposable directory, never real credentials.
    public static ResumeLookup LookupAt(string directory, string endpoint, string profile, int playerId, out Receipt receipt)
    {
        receipt = null;
        if (string.IsNullOrWhiteSpace(profile)) return ResumeLookup.ProfileRequired;
        if (string.IsNullOrWhiteSpace(endpoint) || !Directory.Exists(directory)) return ResumeLookup.None;
        try
        {
            var candidates = new List<Receipt>();
            Receipt newest = null;
            foreach (var file in Directory.GetFiles(directory, Key(endpoint) + ".*.json"))
            {
                Receipt candidate;
                try { candidate = JsonUtility.FromJson<Receipt>(File.ReadAllText(file)); }
                catch (ArgumentException) { continue; }
                if (candidate == null || candidate.Endpoint != endpoint || candidate.MatchSessionId == 0
                    || candidate.Profile != profile || candidate.PlayerId < 0 || playerId >= 0 && candidate.PlayerId != playerId
                    || candidate.Ticket == null || candidate.Ticket.Length != 44
                    || !ValidTicket(candidate.Ticket)) continue;
                candidates.Add(candidate);
                if (newest == null || candidate.SavedUtcTicks > newest.SavedUtcTicks
                    || candidate.SavedUtcTicks == newest.SavedUtcTicks && candidate.MatchSessionId > newest.MatchSessionId) newest = candidate;
            }
            int matching = 0;
            foreach (var candidate in candidates)
                if (candidate.MatchSessionId == newest.MatchSessionId) { matching++; receipt = candidate; }
            if (matching > 1) { receipt = null; LastError = "This profile has several player tickets; choose the player ID explicitly."; return ResumeLookup.Ambiguous; }
            LastError = null;
            return receipt == null ? ResumeLookup.None : ResumeLookup.Found;
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is System.Security.SecurityException)
        { LastError = "The saved resume ticket could not be read."; return ResumeLookup.Unavailable; }
    }

    static bool ValidTicket(string ticket)
    {
        // Validate before constructing a FixedString: a corrupted Unicode value can exceed its byte capacity.
        try { return Convert.FromBase64String(ticket).Length == 32; }
        catch (FormatException) { return false; }
    }

    public static bool SaveAt(string directory, string endpoint, ulong match, FixedString128Bytes ticket, int playerId, string profile)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(profile) || playerId < 0 || match == 0 || !PlayerResumeTicket.TryHash(ticket, out _)) return false;
        string temporary = null;
        try
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, Key(endpoint) + "." + match + "." + playerId + ".json");
            temporary = path + ".tmp";
            var receipt = new Receipt { Endpoint = endpoint, MatchSessionId = match, Ticket = ticket.ToString(), PlayerId = playerId, Profile = profile, SavedUtcTicks = DateTime.UtcNow.Ticks };
            File.WriteAllText(temporary, JsonUtility.ToJson(receipt));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
            LastError = null;
            return true;
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is System.Security.SecurityException)
        { LastError = "The resume ticket could not be saved; rejoin after restarting is unavailable."; return false; }
        finally
        {
            if (temporary != null)
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is System.Security.SecurityException) { }
        }
    }
}
