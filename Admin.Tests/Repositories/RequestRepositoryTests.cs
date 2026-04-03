using ADMIN.Data.Dto;
using ADMIN.Model;
using ADMIN.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Admin.Tests.Repositories
{
    public class RequestRepositoryTests : IDisposable
    {
        private readonly MACUTIONDB _db;
        private readonly RequestRepository _sut;

        public RequestRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<MACUTIONDB>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _db = new MACUTIONDB(options);
            _sut = new RequestRepository(_db);
        }

        public void Dispose()
        {
            _db.Database.EnsureDeleted();
            _db.Dispose();
        }

        private async Task SeedAsync(params RequestTable[] requests)
        {
            _db.REQUESTS.AddRange(requests);
            await _db.SaveChangesAsync();
            // Detach all entities so the repository works with a clean tracker
            foreach (var entry in _db.ChangeTracker.Entries().ToList())
                entry.State = EntityState.Detached;
        }


        [Fact]
        public async Task GetRequestByUserIdAsync_Should_Return_Request_When_Exists()
        {
            // Arrange
            await SeedAsync(new RequestTable
            {
                Id = 1,
                RequestUserId = 100,
                Name = "Alice",
                Email = "alice@test.com",
                VerifierId = 0,
                VerifiedByAdmin = false
            });

            // Act
            var result = await _sut.GetRequestByUserIdAsync(100);

            // Assert
            result.Should().NotBeNull();
            result!.RequestUserId.Should().Be(100);
            result.Name.Should().Be("Alice");
        }

        [Fact]
        public async Task GetRequestByUserIdAsync_Should_Return_Null_When_Not_Exists()
        {
            // Act
            var result = await _sut.GetRequestByUserIdAsync(999);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetRequestByUserIdAsync_Should_Return_First_When_Multiple_Exist()
        {
            // Arrange — two requests with the same RequestUserId (edge case)
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 50, Name = "First", Email = "a@test.com", VerifierId = 0 },
                new RequestTable { Id = 2, RequestUserId = 50, Name = "Second", Email = "b@test.com", VerifierId = 0 }
            );

            // Act
            var result = await _sut.GetRequestByUserIdAsync(50);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().BeOneOf(1, 2);
        }


        [Fact]
        public async Task UpdateRequestAsync_Should_Return_True_When_Entity_Is_Updated()
        {
            // Arrange
            var request = new RequestTable
            {
                Id = 1,
                RequestUserId = 10,
                Name = "Bob",
                Email = "bob@test.com",
                VerifierId = 0,
                VerifiedByAdmin = false
            };
            await SeedAsync(request);

            // Modify the entity
            var toUpdate = await _db.REQUESTS.FindAsync(1);
            toUpdate!.VerifiedByAdmin = true;
            toUpdate.VerifierId = 5;
            toUpdate.VerifiedAt = DateTime.UtcNow;
            _db.ChangeTracker.Clear();

            // Act
            var result = await _sut.UpdateRequestAsync(toUpdate);

            // Assert
            result.Should().BeTrue();

            // Verify the persisted state
            _db.ChangeTracker.Clear();
            var persisted = await _db.REQUESTS.FindAsync(1);
            persisted!.VerifiedByAdmin.Should().BeTrue();
            persisted.VerifierId.Should().Be(5);
            persisted.VerifiedAt.Should().NotBeNull();
        }

        [Fact]
        public async Task UpdateRequestAsync_Should_Return_True_When_Updating_Name_And_Email()
        {
            // Arrange
            await SeedAsync(new RequestTable
            {
                Id = 1,
                RequestUserId = 10,
                Name = "OldName",
                Email = "old@test.com",
                VerifierId = 0
            });

            var toUpdate = await _db.REQUESTS.FindAsync(1);
            toUpdate!.Name = "NewName";
            toUpdate.Email = "new@test.com";
            _db.ChangeTracker.Clear();

            // Act
            var result = await _sut.UpdateRequestAsync(toUpdate);

            // Assert
            result.Should().BeTrue();
            _db.ChangeTracker.Clear();
            var persisted = await _db.REQUESTS.FindAsync(1);
            persisted!.Name.Should().Be("NewName");
            persisted.Email.Should().Be("new@test.com");
        }


        [Fact]
        public async Task GetRequestsByVerifierIdAsync_Should_Return_Matching_Requests()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 5, VerifiedByAdmin = true },
                new RequestTable { Id = 2, RequestUserId = 11, Name = "B", Email = "b@t.com", VerifierId = 5, VerifiedByAdmin = true },
                new RequestTable { Id = 3, RequestUserId = 12, Name = "C", Email = "c@t.com", VerifierId = 9, VerifiedByAdmin = true }
            );

            // Act
            var result = await _sut.GetRequestsByVerifierIdAsync(5);

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(r => r.VerifierId == 5);
        }

        [Fact]
        public async Task GetRequestsByVerifierIdAsync_Should_Return_Empty_When_No_Match()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 5 }
            );

            // Act
            var result = await _sut.GetRequestsByVerifierIdAsync(999);

            // Assert
            result.Should().BeEmpty();
        }


        [Fact]
        public async Task GetPendingRequestsAsync_Should_Return_Only_Unverified_Requests()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 0, VerifiedByAdmin = false },
                new RequestTable { Id = 2, RequestUserId = 11, Name = "B", Email = "b@t.com", VerifierId = 5, VerifiedByAdmin = true },
                new RequestTable { Id = 3, RequestUserId = 12, Name = "C", Email = "c@t.com", VerifierId = 0, VerifiedByAdmin = false }
            );

            // Act
            var result = await _sut.GetPendingRequestsAsync();

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(r => !r.VerifiedByAdmin);
        }

        [Fact]
        public async Task GetPendingRequestsAsync_Should_Return_Empty_When_All_Verified()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 5, VerifiedByAdmin = true }
            );

            // Act
            var result = await _sut.GetPendingRequestsAsync();

            // Assert
            result.Should().BeEmpty();
        }


        [Fact]
        public async Task GetVerifiedRequestsAsync_Should_Return_Only_Verified_Requests()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 0, VerifiedByAdmin = false },
                new RequestTable { Id = 2, RequestUserId = 11, Name = "B", Email = "b@t.com", VerifierId = 5, VerifiedByAdmin = true },
                new RequestTable { Id = 3, RequestUserId = 12, Name = "C", Email = "c@t.com", VerifierId = 6, VerifiedByAdmin = true }
            );

            // Act
            var result = await _sut.GetVerifiedRequestsAsync();

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(r => r.VerifiedByAdmin);
        }

        [Fact]
        public async Task GetVerifiedRequestsAsync_Should_Return_Empty_When_None_Verified()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 0, VerifiedByAdmin = false }
            );

            // Act
            var result = await _sut.GetVerifiedRequestsAsync();

            // Assert
            result.Should().BeEmpty();
        }


        [Fact]
        public async Task GetPendingCountAsync_Should_Return_Count_Of_Unverified_Requests()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 0, VerifiedByAdmin = false },
                new RequestTable { Id = 2, RequestUserId = 11, Name = "B", Email = "b@t.com", VerifierId = 5, VerifiedByAdmin = true },
                new RequestTable { Id = 3, RequestUserId = 12, Name = "C", Email = "c@t.com", VerifierId = 0, VerifiedByAdmin = false },
                new RequestTable { Id = 4, RequestUserId = 13, Name = "D", Email = "d@t.com", VerifierId = 0, VerifiedByAdmin = false }
            );

            // Act
            var count = await _sut.GetPendingCountAsync();

            // Assert
            count.Should().Be(3);
        }

        [Fact]
        public async Task GetPendingCountAsync_Should_Return_Zero_When_All_Verified()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 5, VerifiedByAdmin = true }
            );

            // Act
            var count = await _sut.GetPendingCountAsync();

            // Assert
            count.Should().Be(0);
        }

        [Fact]
        public async Task GetPendingCountAsync_Should_Return_Zero_When_No_Requests()
        {
            // Act
            var count = await _sut.GetPendingCountAsync();

            // Assert
            count.Should().Be(0);
        }


        [Fact]
        public async Task GetVerifiedCountAsync_Should_Return_Count_Of_Verified_Requests()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 0, VerifiedByAdmin = false },
                new RequestTable { Id = 2, RequestUserId = 11, Name = "B", Email = "b@t.com", VerifierId = 5, VerifiedByAdmin = true },
                new RequestTable { Id = 3, RequestUserId = 12, Name = "C", Email = "c@t.com", VerifierId = 6, VerifiedByAdmin = true }
            );

            // Act
            var count = await _sut.GetVerifiedCountAsync();

            // Assert
            count.Should().Be(2);
        }

        [Fact]
        public async Task GetVerifiedCountAsync_Should_Return_Zero_When_None_Verified()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 0, VerifiedByAdmin = false }
            );

            // Act
            var count = await _sut.GetVerifiedCountAsync();

            // Assert
            count.Should().Be(0);
        }

        [Fact]
        public async Task GetVerifiedCountAsync_Should_Return_Zero_When_No_Requests()
        {
            // Act
            var count = await _sut.GetVerifiedCountAsync();

            // Assert
            count.Should().Be(0);
        }


        [Fact]
        public async Task GetFilteredData_Pending_True_Should_Return_Only_Unverified()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 0, VerifiedByAdmin = false },
                new RequestTable { Id = 2, RequestUserId = 11, Name = "B", Email = "b@t.com", VerifierId = 5, VerifiedByAdmin = true }
            );

            var filter = new Filter { pending = true, page = 1, pageSize = 10 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().HaveCount(1);
            result.Should().OnlyContain(r => !r.VerifiedByAdmin);
        }

        [Fact]
        public async Task GetFilteredData_Pending_False_Should_Return_Only_Verified()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 0, VerifiedByAdmin = false },
                new RequestTable { Id = 2, RequestUserId = 11, Name = "B", Email = "b@t.com", VerifierId = 5, VerifiedByAdmin = true },
                new RequestTable { Id = 3, RequestUserId = 12, Name = "C", Email = "c@t.com", VerifierId = 6, VerifiedByAdmin = true }
            );

            var filter = new Filter { pending = false, page = 1, pageSize = 10 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(r => r.VerifiedByAdmin);
        }


        [Fact]
        public async Task GetFilteredData_From_Should_Filter_By_VerifiedAt_GreaterOrEqual()
        {
            // Arrange
            var dateRef = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 5, VerifiedByAdmin = true, VerifiedAt = dateRef.AddDays(-5) },
                new RequestTable { Id = 2, RequestUserId = 11, Name = "B", Email = "b@t.com", VerifierId = 5, VerifiedByAdmin = true, VerifiedAt = dateRef },
                new RequestTable { Id = 3, RequestUserId = 12, Name = "C", Email = "c@t.com", VerifierId = 6, VerifiedByAdmin = true, VerifiedAt = dateRef.AddDays(5) }
            );

            var filter = new Filter { pending = false, From = dateRef, page = 1, pageSize = 10 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(r => r.VerifiedAt >= dateRef);
        }

        [Fact]
        public async Task GetFilteredData_To_Should_Filter_By_VerifiedAt_LessThan_NextDay()
        {
            // Arrange
            var dateRef = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 5, VerifiedByAdmin = true, VerifiedAt = dateRef.AddHours(10) },
                new RequestTable { Id = 2, RequestUserId = 11, Name = "B", Email = "b@t.com", VerifierId = 5, VerifiedByAdmin = true, VerifiedAt = dateRef.AddDays(2) },
                new RequestTable { Id = 3, RequestUserId = 12, Name = "C", Email = "c@t.com", VerifierId = 6, VerifiedByAdmin = true, VerifiedAt = dateRef.AddDays(-1) }
            );

            // To = dateRef means include everything on that day, but not the next day
            var filter = new Filter { pending = false, To = dateRef, page = 1, pageSize = 10 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().HaveCount(2); // Id 1 (same day) and Id 3 (before)
            result.Should().OnlyContain(r => r.VerifiedAt < dateRef.Date.AddDays(1));
        }

        [Fact]
        public async Task GetFilteredData_From_And_To_Combined_Should_Filter_Date_Range()
        {
            // Arrange
            var fromDate = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc);
            var toDate = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 5, VerifiedByAdmin = true, VerifiedAt = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc) },   // before range
                new RequestTable { Id = 2, RequestUserId = 11, Name = "B", Email = "b@t.com", VerifierId = 5, VerifiedByAdmin = true, VerifiedAt = new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc) },  // in range
                new RequestTable { Id = 3, RequestUserId = 12, Name = "C", Email = "c@t.com", VerifierId = 6, VerifiedByAdmin = true, VerifiedAt = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc) }, // in range (same day as To)
                new RequestTable { Id = 4, RequestUserId = 13, Name = "D", Email = "d@t.com", VerifierId = 6, VerifiedByAdmin = true, VerifiedAt = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc) }   // after range
            );

            var filter = new Filter { pending = false, From = fromDate, To = toDate, page = 1, pageSize = 10 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().HaveCount(2); // Id 2 and Id 3
        }


        [Fact]
        public async Task GetFilteredData_Mine_True_Should_Filter_By_VerifierId()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 5, VerifiedByAdmin = true },
                new RequestTable { Id = 2, RequestUserId = 11, Name = "B", Email = "b@t.com", VerifierId = 7, VerifiedByAdmin = true },
                new RequestTable { Id = 3, RequestUserId = 12, Name = "C", Email = "c@t.com", VerifierId = 5, VerifiedByAdmin = true }
            );

            var filter = new Filter { pending = false, mine = true, mineId = 5, page = 1, pageSize = 10 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(r => r.VerifierId == 5);
        }

        [Fact]
        public async Task GetFilteredData_Mine_False_Should_Not_Filter_By_VerifierId()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 5, VerifiedByAdmin = true },
                new RequestTable { Id = 2, RequestUserId = 11, Name = "B", Email = "b@t.com", VerifierId = 7, VerifiedByAdmin = true }
            );

            var filter = new Filter { pending = false, mine = false, mineId = 5, page = 1, pageSize = 10 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().HaveCount(2); // no mine filter applied
        }

        // ============================================================
        //  getFilteredData — pagination
        // ============================================================

        [Fact]
        public async Task GetFilteredData_Pagination_Page1_Should_Return_First_Page()
        {
            // Arrange — seed 5 unverified requests
            var requests = Enumerable.Range(1, 5).Select(i => new RequestTable
            {
                Id = i,
                RequestUserId = 100 + i,
                Name = $"User{i}",
                Email = $"user{i}@t.com",
                VerifierId = 0,
                VerifiedByAdmin = false
            }).ToArray();
            await SeedAsync(requests);

            var filter = new Filter { pending = true, page = 1, pageSize = 2 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetFilteredData_Pagination_Page2_Should_Return_Second_Page()
        {
            // Arrange — seed 5 unverified requests
            var requests = Enumerable.Range(1, 5).Select(i => new RequestTable
            {
                Id = i,
                RequestUserId = 100 + i,
                Name = $"User{i}",
                Email = $"user{i}@t.com",
                VerifierId = 0,
                VerifiedByAdmin = false
            }).ToArray();
            await SeedAsync(requests);

            var filter = new Filter { pending = true, page = 2, pageSize = 2 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetFilteredData_Pagination_LastPage_Should_Return_Remaining_Items()
        {
            // Arrange — seed 5 unverified requests, page size 2 → last page has 1 item
            var requests = Enumerable.Range(1, 5).Select(i => new RequestTable
            {
                Id = i,
                RequestUserId = 100 + i,
                Name = $"User{i}",
                Email = $"user{i}@t.com",
                VerifierId = 0,
                VerifiedByAdmin = false
            }).ToArray();
            await SeedAsync(requests);

            var filter = new Filter { pending = true, page = 3, pageSize = 2 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetFilteredData_Pagination_Beyond_Last_Page_Should_Return_Empty()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 0, VerifiedByAdmin = false }
            );

            var filter = new Filter { pending = true, page = 5, pageSize = 10 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().BeEmpty();
        }

        // ============================================================
        //  getFilteredData — no filters (only pending toggle)
        // ============================================================

        [Fact]
        public async Task GetFilteredData_No_Optional_Filters_Should_Return_All_Matching_Pending_Status()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 0, VerifiedByAdmin = false },
                new RequestTable { Id = 2, RequestUserId = 11, Name = "B", Email = "b@t.com", VerifierId = 0, VerifiedByAdmin = false },
                new RequestTable { Id = 3, RequestUserId = 12, Name = "C", Email = "c@t.com", VerifierId = 5, VerifiedByAdmin = true }
            );

            var filter = new Filter { pending = true, page = 1, pageSize = 10 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().HaveCount(2);
        }

        // ============================================================
        //  getFilteredData — email filter (EF.Functions.Like)
        //  NOTE: EF.Functions.Like is translated to string.Contains by
        //  the InMemory provider, so we test with a substring match.
        // ============================================================

        [Fact]
        public async Task GetFilteredData_Email_Filter_Should_Filter_By_Email_Substring()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "alice@example.com", VerifierId = 0, VerifiedByAdmin = false },
                new RequestTable { Id = 2, RequestUserId = 11, Name = "B", Email = "bob@test.com", VerifierId = 0, VerifiedByAdmin = false },
                new RequestTable { Id = 3, RequestUserId = 12, Name = "C", Email = "alice.smith@example.com", VerifierId = 0, VerifiedByAdmin = false }
            );

            var filter = new Filter { pending = true, email = "alice", page = 1, pageSize = 10 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(r => r.Email.Contains("alice", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task GetFilteredData_Email_Filter_Empty_String_Should_Not_Filter()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "alice@t.com", VerifierId = 0, VerifiedByAdmin = false },
                new RequestTable { Id = 2, RequestUserId = 11, Name = "B", Email = "bob@t.com", VerifierId = 0, VerifiedByAdmin = false }
            );

            var filter = new Filter { pending = true, email = "", page = 1, pageSize = 10 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().HaveCount(2); // empty string → no filter applied
        }

        [Fact]
        public async Task GetFilteredData_Email_Filter_Whitespace_Should_Not_Filter()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "alice@t.com", VerifierId = 0, VerifiedByAdmin = false }
            );

            var filter = new Filter { pending = true, email = "   ", page = 1, pageSize = 10 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().HaveCount(1); // whitespace → no filter applied
        }

        // ============================================================
        //  getFilteredData — name filter (EF.Functions.Like)
        // ============================================================

        [Fact]
        public async Task GetFilteredData_Name_Filter_Should_Filter_By_Name_Substring()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "John Doe", Email = "a@t.com", VerifierId = 0, VerifiedByAdmin = false },
                new RequestTable { Id = 2, RequestUserId = 11, Name = "Jane Smith", Email = "b@t.com", VerifierId = 0, VerifiedByAdmin = false },
                new RequestTable { Id = 3, RequestUserId = 12, Name = "Johnny Appleseed", Email = "c@t.com", VerifierId = 0, VerifiedByAdmin = false }
            );

            var filter = new Filter { pending = true, name = "John", page = 1, pageSize = 10 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(r => r.Name.Contains("John", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task GetFilteredData_Name_Filter_Empty_String_Should_Not_Filter()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 0, VerifiedByAdmin = false },
                new RequestTable { Id = 2, RequestUserId = 11, Name = "B", Email = "b@t.com", VerifierId = 0, VerifiedByAdmin = false }
            );

            var filter = new Filter { pending = true, name = "", page = 1, pageSize = 10 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().HaveCount(2);
        }


        [Fact]
        public async Task GetFilteredData_Combined_Filters_Should_Apply_All_Conditions()
        {
            // Arrange
            var dateRef = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "John Doe", Email = "john@example.com", VerifierId = 5, VerifiedByAdmin = true, VerifiedAt = dateRef },
                new RequestTable { Id = 2, RequestUserId = 11, Name = "John Smith", Email = "john.smith@test.com", VerifierId = 5, VerifiedByAdmin = true, VerifiedAt = dateRef.AddDays(-10) },
                new RequestTable { Id = 3, RequestUserId = 12, Name = "Jane Doe", Email = "jane@example.com", VerifierId = 5, VerifiedByAdmin = true, VerifiedAt = dateRef },
                new RequestTable { Id = 4, RequestUserId = 13, Name = "John Wick", Email = "wick@example.com", VerifierId = 9, VerifiedByAdmin = true, VerifiedAt = dateRef }
            );

            var filter = new Filter
            {
                pending = false,
                name = "John",
                From = dateRef.AddDays(-1),
                mine = true,
                mineId = 5,
                page = 1,
                pageSize = 10
            };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            // Should match Id=1 only: name contains "John", From >= dateRef-1, mine=true & verifierId=5
            // Id=2 is excluded by From date, Id=3 excluded by name, Id=4 excluded by mine filter
            result.Should().HaveCount(1);
            result[0].Id.Should().Be(1);
        }

        [Fact]
        public async Task GetFilteredData_Empty_Database_Should_Return_Empty_List()
        {
            // Arrange
            var filter = new Filter { pending = true, page = 1, pageSize = 10 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().BeEmpty();
        }


        [Fact]
        public async Task GetFilteredData_Null_From_And_To_Should_Not_Filter_By_Date()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 0, VerifiedByAdmin = false, VerifiedAt = null },
                new RequestTable { Id = 2, RequestUserId = 11, Name = "B", Email = "b@t.com", VerifierId = 0, VerifiedByAdmin = false, VerifiedAt = DateTime.UtcNow }
            );

            var filter = new Filter { pending = true, From = null, To = null, page = 1, pageSize = 10 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().HaveCount(2);
        }


        [Fact]
        public async Task GetFilteredData_Email_Filter_No_Match_Should_Return_Empty()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "alice@t.com", VerifierId = 0, VerifiedByAdmin = false }
            );

            var filter = new Filter { pending = true, email = "zznoexist", page = 1, pageSize = 10 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().BeEmpty();
        }


        [Fact]
        public async Task GetFilteredData_Name_Filter_No_Match_Should_Return_Empty()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "Alice", Email = "a@t.com", VerifierId = 0, VerifiedByAdmin = false }
            );

            var filter = new Filter { pending = true, name = "Zznoexist", page = 1, pageSize = 10 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetFilteredData_Mine_True_No_Matching_VerifierId_Should_Return_Empty()
        {
            // Arrange
            await SeedAsync(
                new RequestTable { Id = 1, RequestUserId = 10, Name = "A", Email = "a@t.com", VerifierId = 5, VerifiedByAdmin = true }
            );

            var filter = new Filter { pending = false, mine = true, mineId = 999, page = 1, pageSize = 10 };

            // Act
            var result = await _sut.getFilteredData(filter);

            // Assert
            result.Should().BeEmpty();
        }
    }
}
