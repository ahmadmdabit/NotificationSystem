using System.Data;

using Microsoft.Data.SqlClient.Server;

using NotificationService.Domain;
using NotificationService.Infrastructure.Data.Tvp;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace NotificationService.Tests.Infrastructure;

public class TvpStreamingTests
{
    [Test]
    public async Task Definition_MetadataAndTypeName_MatchSqlContract()
    {
        // Act
        var definition = NotificationHistoryTvpDefinition.Instance;

        // Assert — the type name is the SQL contract; a rename orphans the TVP
        await Assert.That(definition.TypeName).IsEqualTo("[dbo].[TypeNotificationHistory]");
        await Assert.That(definition.Metadata).Count().IsEqualTo(2);
        await Assert.That(definition.Metadata[0].Name).IsEqualTo("NotificationId");
        await Assert.That(definition.Metadata[0].SqlDbType).IsEqualTo(SqlDbType.BigInt);
        await Assert.That(definition.Metadata[1].Name).IsEqualTo("UserId");
        await Assert.That(definition.Metadata[1].SqlDbType).IsEqualTo(SqlDbType.BigInt);
    }

    [Test]
    public async Task PopulateRecord_SetsOrdinalsCorrectly()
    {
        // Arrange — ordinal order must match the Metadata order exactly
        var definition = NotificationHistoryTvpDefinition.Instance;
        var record = new SqlDataRecord(definition.Metadata);
        var entity = new NotificationHistory { NotificationId = 7, UserId = 70 };

        // Act
        definition.PopulateRecord(record, in entity);

        // Assert
        await Assert.That(record.GetInt64(0)).IsEqualTo(7L);
        await Assert.That(record.GetInt64(1)).IsEqualTo(70L);
    }

    [Test]
    public async Task AsSqlDataRecords_WhenSourceIsNull_ReturnsEmptySequence()
    {
        // Act — a null source must not throw; the SP must receive an empty TVP
        var records = ((IEnumerable<NotificationHistory>?)null)
            .AsSqlDataRecords(NotificationHistoryTvpDefinition.Instance);

        // Assert
        await Assert.That(records).IsEmpty();
    }

    [Test]
    public async Task AsSqlDataRecords_ReusesBufferAndStreamsEntities()
    {
        // Arrange
        var definition = NotificationHistoryTvpDefinition.Instance;
        var source = new[]
        {
            new NotificationHistory { NotificationId = 1, UserId = 10 },
            new NotificationHistory { NotificationId = 2, UserId = 20 },
            new NotificationHistory { NotificationId = 3, UserId = 30 }
        };

        // Act — enumerate lazily, reading each record BEFORE advancing, which is how
        // SqlClient consumes a TVP. Buffering the sequence is a different (unsafe) use.
        var observed = new List<(long NotificationId, long UserId)>();
        foreach (var record in source.AsSqlDataRecords(definition))
        {
            observed.Add((record.GetInt64(0), record.GetInt64(1)));
        }

        // Assert
        await Assert.That(observed).Count().IsEqualTo(3);
        await Assert.That(observed[0]).IsEqualTo((1L, 10L));
        await Assert.That(observed[1]).IsEqualTo((2L, 20L));
        await Assert.That(observed[2]).IsEqualTo((3L, 30L));
    }

    [Test]
    public async Task AsSqlDataRecords_YieldsOneSharedRecordInstance_ByDesign()
    {
        // The single reused buffer is the zero-allocation optimisation the XML comment
        // claims. It also means every yielded element is the SAME SqlDataReference:
        // materialising the sequence (ToList/ToArray) collapses it to N copies of the
        // LAST entity. Safe for its only consumer (SqlClient, which enumerates lazily
        // and reads each record before advancing); unsafe if anyone ever buffers it.
        // Verified: 3 entities materialised => all three read 3/30.
        var source = new[]
        {
            new NotificationHistory { NotificationId = 1, UserId = 10 },
            new NotificationHistory { NotificationId = 2, UserId = 20 },
            new NotificationHistory { NotificationId = 3, UserId = 30 }
        };

        var materialised = source.AsSqlDataRecords(NotificationHistoryTvpDefinition.Instance).ToList();

        // The count is right; the CONTENT is not, and that is the documented contract
        await Assert.That(materialised).Count().IsEqualTo(3);
        await Assert.That(materialised[0]).IsSameReferenceAs(materialised[2]);
        await Assert.That(materialised[0].GetInt64(0)).IsEqualTo(3L);
    }

    [Test]
    public async Task AsSqlDataRecords_SkipsNullItems()
    {
        // Arrange — a null element in the batch must be skipped, not passed to PopulateRecord
        // ITvpDefinition<T> is invariant, so the array stays NotificationHistory[] and the
        // null slot is forced with !. The runtime value really is null, which is what
        // exercises the item is null skip inside AsSqlDataRecords.
        NotificationHistory? missing = null;
        var source = new[]
        {
            new NotificationHistory { NotificationId = 1, UserId = 10 },
            missing!,
            new NotificationHistory { NotificationId = 2, UserId = 20 }
        };

        // Act
        var observed = new List<long>();
        foreach (var record in source.AsSqlDataRecords(NotificationHistoryTvpDefinition.Instance))
        {
            observed.Add(record.GetInt64(0));
        }

        // Assert
        await Assert.That(observed).Count().IsEqualTo(2);
        await Assert.That(observed[0]).IsEqualTo(1L);
        await Assert.That(observed[1]).IsEqualTo(2L);
    }
}