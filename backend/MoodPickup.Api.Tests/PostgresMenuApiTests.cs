using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.DTOs.Audit;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Menu.Admin;
using MoodPickup.Api.DTOs.Menu.Public;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure;

namespace MoodPickup.Api.Tests;

public sealed class PostgresMenuApiTests(PostgresMoodPickupApiFactory factory)
    : IClassFixture<PostgresMoodPickupApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [PostgresFact]
    public async Task PublicMenu_ProjectsVisibilityPaginationSearchAndConfiguration()
    {
        await factory.ResetAsync();
        using var client = factory.CreateSecureClient();

        var categories = await client.GetFromJsonAsync<List<PublicCategoryDto>>(
            "/api/v1/categories");
        Assert.NotNull(categories);
        Assert.Equal(["Coffee"], categories.Select(category => category.Name));

        var firstPage = await client.GetFromJsonAsync<
            PagedResponse<PublicProductListItemDto>>(
            "/api/v1/products?page=1&pageSize=2");
        Assert.NotNull(firstPage);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.TotalPages);

        var search = await client.GetFromJsonAsync<
            PagedResponse<PublicProductListItemDto>>(
            "/api/v1/products?search=SEASONAL");
        Assert.NotNull(search);
        var unavailable = Assert.Single(search.Items);
        Assert.False(unavailable.IsAvailable);
        Assert.False(unavailable.IsOrderable);
        Assert.Contains(
            unavailable.AvailabilityIssues,
            issue => issue.Code == "PRODUCT_UNAVAILABLE");

        var cappuccino = await factory.ReadDatabaseAsync(db => db.Products
            .IgnoreQueryFilters()
            .SingleAsync(product => product.Name == "Cappuccino"));
        var detail = await client.GetFromJsonAsync<PublicProductDetailDto>(
            $"/api/v1/products/{cappuccino.Id}");
        Assert.NotNull(detail);
        Assert.Equal(24m, detail.PriceFrom);
        var size = Assert.Single(detail.OptionGroups);
        Assert.Equal(["Small", "Large"], size.Values.Select(value => value.Name));
        Assert.False(size.Values.Single(value => value.Name == "Large").IsAvailable);

        var hiddenProductId = await factory.ReadDatabaseAsync(db => db.Products
            .IgnoreQueryFilters()
            .Where(product => product.Name == "Hidden Product")
            .Select(product => product.Id)
            .SingleAsync());
        using var hiddenResponse = await client.GetAsync(
            $"/api/v1/products/{hiddenProductId}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenResponse.StatusCode);
    }

    [PostgresFact]
    public async Task PublicMenu_UsesServerFiltersAndProjectsOnlyCustomerFields()
    {
        await factory.ResetAsync();
        using var client = factory.CreateSecureClient();
        var coffee = await factory.ReadDatabaseAsync(db => db.Categories
            .AsNoTracking()
            .SingleAsync(category => category.Name == "Coffee"));

        using var listResponse = await client.GetAsync(
            $"/api/v1/products?categoryId={coffee.Id}" +
            "&search=STEAMED&includeUnavailable=false&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listDocument = JsonDocument.Parse(
            await listResponse.Content.ReadAsStringAsync());
        var item = Assert.Single(
            listDocument.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("Cappuccino", item.GetProperty("name").GetString());
        Assert.Equal(
            [
                "availabilityIssues",
                "categoryId",
                "currency",
                "id",
                "isAvailable",
                "isOrderable",
                "name",
                "priceFrom",
                "shortDescription"
            ],
            item.EnumerateObject()
                .Select(property => property.Name)
                .Order()
                .ToArray());

        var productId = item.GetProperty("id").GetGuid();
        using var detailResponse = await client.GetAsync(
            $"/api/v1/products/{productId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        using var detailDocument = JsonDocument.Parse(
            await detailResponse.Content.ReadAsStringAsync());
        var detail = detailDocument.RootElement;
        Assert.Equal(
            [
                "availabilityIssues",
                "basePrice",
                "categoryId",
                "currency",
                "description",
                "id",
                "isAvailable",
                "isOrderable",
                "name",
                "optionGroups",
                "priceFrom"
            ],
            detail.EnumerateObject()
                .Select(property => property.Name)
                .Order()
                .ToArray());

        var rawDetail = detail.GetRawText();
        Assert.DoesNotContain("rowVersion", rawDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("storageKey", rawDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("isDeleted", rawDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("createdAt", rawDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("updatedAt", rawDetail, StringComparison.Ordinal);
    }

    [PostgresFact]
    public async Task AdminMenu_AuthorizationMatrixIsEnforced()
    {
        await factory.ResetAsync();
        using var client = factory.CreateSecureClient();

        using var anonymous = await client.GetAsync("/api/v1/admin/categories");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var customerToken = await factory.CreateCustomerTokenAsync();
        using var customer = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/v1/admin/categories",
            customerToken);
        Assert.Equal(HttpStatusCode.Forbidden, customer.StatusCode);

        var kitchenToken = await factory.CreateEmployeeTokenAsync(
            "kitchen-api",
            AuthenticationConstants.Roles.Kitchen);
        using var kitchen = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/v1/admin/categories",
            kitchenToken);
        Assert.Equal(HttpStatusCode.Forbidden, kitchen.StatusCode);

        var managerToken = await factory.CreateEmployeeTokenAsync(
            "menu-api",
            AuthenticationConstants.Roles.MenuManager);
        using var manager = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/v1/admin/categories",
            managerToken);
        Assert.Equal(HttpStatusCode.OK, manager.StatusCode);

        var administratorToken = await factory.GetAdministratorTokenAsync();
        using var administrator = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/v1/admin/categories",
            administratorToken);
        Assert.Equal(HttpStatusCode.OK, administrator.StatusCode);
    }

    [PostgresFact]
    public async Task Categories_SupportLifecycleAtomicReorderConcurrencyAndAudit()
    {
        await factory.ResetAsync();
        using var client = factory.CreateSecureClient();
        var token = await factory.GetAdministratorTokenAsync();

        using var createResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/admin/categories",
            token,
            new CreateCategoryRequest("Bakery", "Baked food", 10, true));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);
        var created = await ReadAsync<AdminCategoryDto>(createResponse);

        using var updateResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/admin/categories/{created.Id}",
            token,
            new UpdateCategoryRequest(
                "Bakery Updated",
                "Updated",
                11,
                true,
                created.RowVersion));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await ReadAsync<AdminCategoryDto>(updateResponse);
        Assert.NotEqual(created.RowVersion, updated.RowVersion);

        using var staleResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/admin/categories/{created.Id}",
            token,
            new UpdateCategoryRequest(
                "Stale overwrite",
                null,
                12,
                true,
                created.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        Assert.Equal("MENU_VERSION_CONFLICT", await ProblemCodeAsync(staleResponse));
        Assert.Contains(
            "\"currentResource\"",
            await staleResponse.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        var coffee = await GetCategoryByNameAsync(client, token, "Coffee");
        using var reorderResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Put,
            "/api/v1/admin/categories/reorder",
            token,
            new ReorderCategoriesRequest(
            [
                new ReorderItemRequest(updated.Id, 1, updated.RowVersion),
                new ReorderItemRequest(coffee.Id, 2, coffee.RowVersion)
            ]));
        Assert.Equal(HttpStatusCode.OK, reorderResponse.StatusCode);
        var reordered = await ReadAsync<List<AdminCategoryDto>>(reorderResponse);
        var reorderedBakery = reordered.Single(category => category.Id == updated.Id);
        var reorderedCoffee = reordered.Single(category => category.Id == coffee.Id);

        using var failedReorder = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Put,
            "/api/v1/admin/categories/reorder",
            token,
            new ReorderCategoriesRequest(
            [
                new ReorderItemRequest(reorderedBakery.Id, 50, updated.RowVersion),
                new ReorderItemRequest(
                    reorderedCoffee.Id,
                    51,
                    reorderedCoffee.RowVersion)
            ]));
        Assert.Equal(HttpStatusCode.Conflict, failedReorder.StatusCode);
        var unchangedCoffee = await GetCategoryByNameAsync(client, token, "Coffee");
        Assert.Equal(2, unchangedCoffee.DisplayOrder);

        using var visibilityResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Patch,
            $"/api/v1/admin/categories/{reorderedBakery.Id}/visibility",
            token,
            new SetVisibilityRequest(false, reorderedBakery.RowVersion));
        var hidden = await ReadAsync<AdminCategoryDto>(visibilityResponse);
        Assert.False(hidden.IsVisible);

        using var deleteResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Delete,
            $"/api/v1/admin/categories/{hidden.Id}?rowVersion={hidden.RowVersion}",
            token);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        var deleted = await GetCategoryAsync(client, token, hidden.Id);
        Assert.True(deleted.IsDeleted);

        using var restoreResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/categories/{deleted.Id}/restore",
            token,
            new RowVersionRequest(deleted.RowVersion));
        Assert.Equal(HttpStatusCode.OK, restoreResponse.StatusCode);
        Assert.False((await ReadAsync<AdminCategoryDto>(restoreResponse)).IsDeleted);

        var auditCount = await factory.ReadDatabaseAsync(db =>
            db.EmployeeActionLogs.CountAsync(log => log.EntityId == created.Id));
        Assert.True(auditCount >= 5);
    }

    [PostgresFact]
    public async Task Products_SupportMutationDuplicationAssignmentsAndAuditRollback()
    {
        await factory.ResetAsync();
        using var client = factory.CreateSecureClient();
        var token = await factory.GetAdministratorTokenAsync();
        var coffee = await GetCategoryByNameAsync(client, token, "Coffee");

        using var createResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/admin/products",
            token,
            new CreateProductRequest(
                coffee.Id,
                "Flat White",
                "Double espresso",
                "Double espresso with milk",
                "Coffee, milk",
                26m,
                null,
                220,
                110,
                null,
                true,
                true,
                20));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await ReadAsync<MenuMutationResponse<AdminProductDto>>(
            createResponse);
        Assert.True(created.Orderability.IsOrderable);

        using var staleUpdate = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/admin/products/{created.Resource.Id}",
            token,
            new UpdateProductRequest(
                coffee.Id,
                "Flat White",
                null,
                null,
                null,
                27m,
                null,
                220,
                110,
                null,
                true,
                true,
                20,
                Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);

        var mediaId = await factory.ReadDatabaseAsync(async db =>
        {
            var media = new MediaFile
            {
                Id = Guid.NewGuid(),
                StorageProvider = "Local",
                StorageKey = "tests/flat-white.jpg",
                OriginalFileName = "flat-white.jpg",
                ContentType = "image/jpeg",
                FileSizeBytes = 100
            };
            db.MediaFiles.Add(media);
            await db.SaveChangesAsync();
            return media.Id;
        });
        using var invalidImageResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/admin/products/{created.Resource.Id}/image",
            token,
            new AssignProductImageRequest(
                Guid.NewGuid(),
                created.Resource.RowVersion));
        Assert.Equal(HttpStatusCode.NotFound, invalidImageResponse.StatusCode);

        using var imageResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/admin/products/{created.Resource.Id}/image",
            token,
            new AssignProductImageRequest(mediaId, created.Resource.RowVersion));
        Assert.Equal(HttpStatusCode.OK, imageResponse.StatusCode);
        var withImage = await ReadAsync<MenuMutationResponse<AdminProductDto>>(
            imageResponse);
        Assert.Equal(mediaId, withImage.Resource.ImageId);

        using var updateResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/admin/products/{withImage.Resource.Id}",
            token,
            new UpdateProductRequest(
                coffee.Id,
                "Flat White Updated",
                withImage.Resource.ShortDescription,
                withImage.Resource.Description,
                withImage.Resource.Ingredients,
                27m,
                null,
                220,
                110,
                mediaId,
                true,
                true,
                20,
                withImage.Resource.RowVersion));
        var updated = await ReadAsync<MenuMutationResponse<AdminProductDto>>(
            updateResponse);
        Assert.Equal(27m, updated.Resource.BasePrice);

        using var availabilityResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Patch,
            $"/api/v1/admin/products/{updated.Resource.Id}/availability",
            token,
            new SetAvailabilityRequest(false, updated.Resource.RowVersion));
        var unavailable = await ReadAsync<MenuMutationResponse<AdminProductDto>>(
            availabilityResponse);
        Assert.False(unavailable.Orderability.IsOrderable);

        using var visibilityResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Patch,
            $"/api/v1/admin/products/{unavailable.Resource.Id}/visibility",
            token,
            new SetVisibilityRequest(false, unavailable.Resource.RowVersion));
        var hidden = await ReadAsync<MenuMutationResponse<AdminProductDto>>(
            visibilityResponse);
        Assert.False(hidden.Resource.IsVisible);

        var cappuccino = await GetAdminProductByNameAsync(
            client,
            token,
            "Cappuccino");
        using var reorderResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Put,
            "/api/v1/admin/products/reorder",
            token,
            new ReorderProductsRequest(
                coffee.Id,
            [
                new ReorderItemRequest(
                    hidden.Resource.Id,
                    1,
                    hidden.Resource.RowVersion),
                new ReorderItemRequest(
                    cappuccino.Id,
                    2,
                    cappuccino.RowVersion)
            ]));
        Assert.Equal(HttpStatusCode.OK, reorderResponse.StatusCode);

        var reorderedFlatWhite = await GetAdminProductByNameAsync(
            client,
            token,
            "Flat White Updated");
        using var deleteResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Delete,
            $"/api/v1/admin/products/{reorderedFlatWhite.Id}" +
            $"?rowVersion={reorderedFlatWhite.RowVersion}",
            token);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        var deletedFlatWhite = await GetAdminProductByNameAsync(
            client,
            token,
            "Flat White Updated");
        Assert.True(deletedFlatWhite.IsDeleted);
        using var restoreResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/products/{deletedFlatWhite.Id}/restore",
            token,
            new RowVersionRequest(deletedFlatWhite.RowVersion));
        Assert.Equal(HttpStatusCode.OK, restoreResponse.StatusCode);

        cappuccino = await GetAdminProductByNameAsync(
            client,
            token,
            "Cappuccino");
        using var duplicateResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/products/{cappuccino.Id}/duplicate",
            token,
            new DuplicateProductRequest("Cappuccino API Copy"));
        Assert.Equal(HttpStatusCode.Created, duplicateResponse.StatusCode);
        var duplicate = await ReadAsync<MenuMutationResponse<AdminProductDto>>(
            duplicateResponse);
        Assert.Equal(
            cappuccino.OptionGroups.SelectMany(group => group.Values).Count(),
            duplicate.Resource.OptionGroups.SelectMany(group => group.Values).Count());

        factory.FailAuditWrites = true;
        using var failedDuplicate = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/products/{cappuccino.Id}/duplicate",
            token,
            new DuplicateProductRequest("Must Roll Back"));
        Assert.Equal(
            HttpStatusCode.InternalServerError,
            failedDuplicate.StatusCode);
        factory.FailAuditWrites = false;
        var rolledBackCount = await factory.ReadDatabaseAsync(db => db.Products
            .IgnoreQueryFilters()
            .CountAsync(product => product.Name == "Must Roll Back"));
        Assert.Equal(0, rolledBackCount);

        var auditCount = await factory.ReadDatabaseAsync(db =>
            db.EmployeeActionLogs.CountAsync(log => log.EntityType == "Product"));
        Assert.True(auditCount >= 3);
    }

    [PostgresFact]
    public async Task OptionDefinitionsAndAssignments_AllowDraftsAndRejectStructuralErrors()
    {
        await factory.ResetAsync();
        using var client = factory.CreateSecureClient();
        var token = await factory.GetAdministratorTokenAsync();

        using var invalidGroup = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/admin/option-groups",
            token,
            new CreateOptionGroupRequest(
                "Invalid",
                null,
                OptionSelectionType.Single,
                true,
                1,
                2,
                0,
                true));
        Assert.Equal(HttpStatusCode.BadRequest, invalidGroup.StatusCode);

        using var groupResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/admin/option-groups",
            token,
            new CreateOptionGroupRequest(
                "Roast",
                null,
                OptionSelectionType.Single,
                true,
                1,
                1,
                5,
                true));
        var group = await ReadAsync<AdminOptionGroupDto>(groupResponse);

        using var updateGroupResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/admin/option-groups/{group.Id}",
            token,
            new UpdateOptionGroupRequest(
                group.Name,
                "Updated group",
                group.SelectionType,
                group.DefaultIsRequired,
                group.DefaultMinimumSelections,
                group.DefaultMaximumSelections,
                group.DisplayOrder,
                group.IsActive,
                group.RowVersion));
        var updatedGroup = await ReadAsync<AdminOptionGroupDto>(updateGroupResponse);
        using var staleGroupResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/admin/option-groups/{group.Id}",
            token,
            new UpdateOptionGroupRequest(
                group.Name,
                null,
                group.SelectionType,
                group.DefaultIsRequired,
                group.DefaultMinimumSelections,
                group.DefaultMaximumSelections,
                group.DisplayOrder,
                group.IsActive,
                group.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, staleGroupResponse.StatusCode);
        Assert.Equal(
            "MENU_VERSION_CONFLICT",
            await ProblemCodeAsync(staleGroupResponse));

        using var valueResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/option-groups/{updatedGroup.Id}/values",
            token,
            new CreateOptionValueRequest("Dark", null, 1, true));
        var value = await ReadAsync<AdminOptionValueDto>(valueResponse);
        using var duplicateValue = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/option-groups/{updatedGroup.Id}/values",
            token,
            new CreateOptionValueRequest("dark", null, 2, true));
        Assert.Equal(HttpStatusCode.Conflict, duplicateValue.StatusCode);
        Assert.Equal(
            "DUPLICATE_OPTION_VALUE_NAME",
            await ProblemCodeAsync(duplicateValue));

        var cappuccino = await GetAdminProductByNameAsync(
            client,
            token,
            "Cappuccino");
        using var assignmentResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/products/{cappuccino.Id}/option-groups",
            token,
            new CreateProductOptionGroupRequest(
                updatedGroup.Id,
                true,
                1,
                1,
                10,
                true));
        Assert.True(
            assignmentResponse.StatusCode == HttpStatusCode.Created,
            await assignmentResponse.Content.ReadAsStringAsync());
        var assignment =
            await ReadAsync<MenuMutationResponse<AdminProductOptionGroupDto>>(
                assignmentResponse);
        Assert.False(assignment.Orderability.IsOrderable);
        Assert.Contains(
            assignment.Orderability.Issues,
            issue => issue.Code ==
                     "REQUIRED_SINGLE_GROUP_HAS_NO_AVAILABLE_DEFAULT");

        using var updateAssignmentResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/admin/products/{cappuccino.Id}/option-groups/" +
            assignment.Resource.Id,
            token,
            new UpdateProductOptionGroupRequest(
                true,
                1,
                1,
                11,
                true,
                assignment.Resource.RowVersion));
        var updatedAssignment =
            await ReadAsync<MenuMutationResponse<AdminProductOptionGroupDto>>(
                updateAssignmentResponse);
        using var staleAssignmentResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/admin/products/{cappuccino.Id}/option-groups/" +
            assignment.Resource.Id,
            token,
            new UpdateProductOptionGroupRequest(
                true,
                1,
                1,
                12,
                true,
                assignment.Resource.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, staleAssignmentResponse.StatusCode);

        var unrelatedValueId = cappuccino.OptionGroups
            .Single(item => item.OptionGroupName == "Size")
            .Values.First().OptionValueId;
        using var mismatch = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/products/{cappuccino.Id}/option-groups/" +
            $"{updatedAssignment.Resource.Id}/values",
            token,
            new CreateProductOptionValueRequest(
                unrelatedValueId,
                0,
                true,
                true,
                1,
                null,
                null));
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        Assert.Equal("OPTION_VALUE_GROUP_MISMATCH", await ProblemCodeAsync(mismatch));

        using var addValue = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/products/{cappuccino.Id}/option-groups/" +
            $"{updatedAssignment.Resource.Id}/values",
            token,
            new CreateProductOptionValueRequest(
                value.Id,
                1m,
                true,
                true,
                1,
                null,
                null));
        var assigned =
            await ReadAsync<MenuMutationResponse<AdminProductOptionValueDto>>(
                addValue);
        Assert.True(assigned.Orderability.IsOrderable);

        using var duplicateAssignment = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/products/{cappuccino.Id}/option-groups/" +
            $"{updatedAssignment.Resource.Id}/values",
            token,
            new CreateProductOptionValueRequest(
                value.Id,
                2m,
                false,
                true,
                2,
                null,
                null));
        Assert.Equal(HttpStatusCode.Conflict, duplicateAssignment.StatusCode);
        Assert.Equal(
            "PRODUCT_OPTION_VALUE_ALREADY_ASSIGNED",
            await ProblemCodeAsync(duplicateAssignment));

        using var updateAssignedValue = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/admin/products/{cappuccino.Id}/option-values/" +
            assigned.Resource.Id,
            token,
            new UpdateProductOptionValueRequest(
                2m,
                true,
                true,
                2,
                null,
                null,
                assigned.Resource.RowVersion));
        var updatedAssignedValue =
            await ReadAsync<MenuMutationResponse<AdminProductOptionValueDto>>(
                updateAssignedValue);
        Assert.Equal(2m, updatedAssignedValue.Resource.PriceModifier);
        using var staleAssignedValue = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/admin/products/{cappuccino.Id}/option-values/" +
            assigned.Resource.Id,
            token,
            new UpdateProductOptionValueRequest(
                3m,
                true,
                true,
                3,
                null,
                null,
                assigned.Resource.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, staleAssignedValue.StatusCode);

        using var deactivateValue = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Patch,
            $"/api/v1/admin/option-values/{value.Id}/active",
            token,
            new SetActiveRequest(false, value.RowVersion));
        var deactivated = await ReadAsync<AdminOptionValueDto>(deactivateValue);
        Assert.False(deactivated.IsActive);
        using var deleteValue = await SendAuthorizedAsync(
            client,
            HttpMethod.Delete,
            $"/api/v1/admin/option-values/{value.Id}" +
            $"?rowVersion={deactivated.RowVersion}",
            token);
        Assert.Equal(HttpStatusCode.NoContent, deleteValue.StatusCode);
        using var valuesResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/api/v1/admin/option-groups/{updatedGroup.Id}/values" +
            "?includeDeleted=true",
            token);
        var values = await ReadAsync<List<AdminOptionValueDto>>(valuesResponse);
        var deletedValue = values.Single(item => item.Id == value.Id);
        Assert.True(deletedValue.IsDeleted);
        using var restoreValue = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/option-values/{value.Id}/restore",
            token,
            new RowVersionRequest(deletedValue.RowVersion));
        Assert.Equal(HttpStatusCode.OK, restoreValue.StatusCode);

        var auditEntries = await factory.ReadDatabaseAsync(db =>
            db.EmployeeActionLogs.CountAsync(log =>
                log.EntityType == "ProductOptionValue"));
        Assert.True(auditEntries >= 1);
    }

    [PostgresFact]
    public async Task AuditApi_UsesAdministratorPolicyFiltersAndHidesJsonFromList()
    {
        await factory.ResetAsync();
        using var client = factory.CreateSecureClient();
        var administratorToken = await factory.GetAdministratorTokenAsync();
        using var mutation = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/admin/categories",
            administratorToken,
            new CreateCategoryRequest("Audit Test", null, 50, true));
        var category = await ReadAsync<AdminCategoryDto>(mutation);

        var managerToken = await factory.CreateEmployeeTokenAsync(
            "audit-menu-manager",
            AuthenticationConstants.Roles.MenuManager);
        using var forbidden = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/v1/admin/audit-log",
            managerToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var listResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/api/v1/admin/audit-log?entityType=Category&entityId={category.Id}",
            administratorToken);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var rawList = await listResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("oldValuesJson", rawList, StringComparison.Ordinal);
        Assert.DoesNotContain("newValuesJson", rawList, StringComparison.Ordinal);
        Assert.DoesNotContain("token", rawList, StringComparison.OrdinalIgnoreCase);
        var list = JsonSerializer.Deserialize<PagedResponse<AuditLogListItemDto>>(
            rawList,
            JsonOptions);
        var audit = Assert.Single(list!.Items);

        using var detailResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/api/v1/admin/audit-log/{audit.Id}",
            administratorToken);
        var detail = await ReadAsync<AuditLogDetailDto>(detailResponse);
        Assert.Contains("Audit Test", detail.NewValuesJson, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "password",
            detail.NewValuesJson,
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<AdminCategoryDto> GetCategoryByNameAsync(
        HttpClient client,
        string token,
        string name)
    {
        using var response = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/api/v1/admin/categories?includeDeleted=true&search={name}",
            token);
        var page = await ReadAsync<PagedResponse<AdminCategoryDto>>(response);
        return page.Items.Single(category => category.Name == name);
    }

    private static async Task<AdminCategoryDto> GetCategoryAsync(
        HttpClient client,
        string token,
        Guid id)
    {
        using var response = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/api/v1/admin/categories/{id}",
            token);
        return await ReadAsync<AdminCategoryDto>(response);
    }

    private static async Task<AdminProductDto> GetAdminProductByNameAsync(
        HttpClient client,
        string token,
        string name)
    {
        using var listResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/api/v1/admin/products?includeDeleted=true&search={name}",
            token);
        var list = await ReadAsync<PagedResponse<AdminProductListItemDto>>(
            listResponse);
        var item = list.Items.Single(product => product.Name == name);
        using var detailResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/api/v1/admin/products/{item.Id}",
            token);
        return await ReadAsync<AdminProductDto>(detailResponse);
    }

    private static async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string token)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendAuthorizedJsonAsync<T>(
        HttpClient client,
        HttpMethod method,
        string path,
        string token,
        T body)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(raw, JsonOptions)
               ?? throw new InvalidOperationException(
                   $"Response did not contain {typeof(T).Name}: {raw}");
    }

    private static async Task<string?> ProblemCodeAsync(
        HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code)
            ? code.GetString()
            : null;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
