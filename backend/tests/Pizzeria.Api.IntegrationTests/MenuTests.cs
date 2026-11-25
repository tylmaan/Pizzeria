using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pizzeria.Api.IntegrationTests.Infrastructure;
using Pizzeria.Application.Menu;
using Pizzeria.Domain.Identity;
using Pizzeria.Domain.Menu;

namespace Pizzeria.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class MenuTests(PizzeriaApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task CompanyAdmin_BuildsMenu_AndStaffCanReadIt()
    {
        var tenant = await factory.CreateCompanyAsync("menu");
        var (pizza, sauces, margherita) = await BuildPizzaMenuAsync(tenant.Admin);
        var location = await tenant.Admin.CreateLocationAsync("Centrum");
        using var staff = await factory.LoginAsync(await tenant.Admin.CreateUserAsync(Roles.Staff, location.Id));

        var menu = await staff.GetFromJsonAsync<MenuDto>("/api/menu", Json);

        Assert.Equal(HalfAndHalfPricing.MoreExpensiveHalf, menu!.HalfAndHalfPricing);
        Assert.Equal(["32 cm", "42 cm"], Assert.Single(menu.Categories).Sizes.Select(s => s.Name));
        var product = Assert.Single(menu.Products);
        Assert.Equal(margherita.Id, product.Id);
        Assert.Equal([25.5m, 32m], product.Prices.OrderBy(p => p.Price).Select(p => p.Price));
        Assert.Equal([sauces.Id], product.ModifierGroupIds);
        Assert.Equal(["Czosnkowy", "Pomidorowy"], Assert.Single(menu.ModifierGroups).Modifiers.Select(m => m.Name));
        Assert.Equal(pizza.Id, menu.ModifierGroups[0].CategoryId);
    }

    [Fact]
    public async Task Staff_CannotEditMenu()
    {
        var tenant = await factory.CreateCompanyAsync("menu");
        var location = await tenant.Admin.CreateLocationAsync("Centrum");
        using var staff = await factory.LoginAsync(await tenant.Admin.CreateUserAsync(Roles.Staff, location.Id));

        using var response = await staff.PostAsJsonAsync("/api/menu/categories", new SaveCategoryRequest("Pizza", [new(null, "32 cm")]), Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OtherCompany_CannotSeeOrReferenceMenu()
    {
        var a = await factory.CreateCompanyAsync("menua");
        var b = await factory.CreateCompanyAsync("menub");
        var (pizzaA, saucesA, margheritaA) = await BuildPizzaMenuAsync(a.Admin);

        var menuB = await b.Admin.GetFromJsonAsync<MenuDto>("/api/menu", Json);
        using var productInForeignCategory = await b.Admin.PostAsJsonAsync("/api/menu/products",
            new SaveProductRequest(pizzaA.Id, "Kradziona", null, false, true, [new(pizzaA.Sizes[0].Id, 10m)], []), Json);
        using var updateForeignProduct = await b.Admin.PutAsJsonAsync($"/api/menu/products/{margheritaA.Id}",
            new SaveProductRequest(pizzaA.Id, "Hacked", null, false, true, [new(pizzaA.Sizes[0].Id, 1m)], []), Json);
        var (pizzaB, _, _) = await BuildPizzaMenuAsync(b.Admin);
        using var foreignGroup = await b.Admin.PostAsJsonAsync("/api/menu/products",
            new SaveProductRequest(pizzaB.Id, "Z cudzym sosem", null, false, true, [new(pizzaB.Sizes[0].Id, 10m)], [saucesA.Id]), Json);

        Assert.Empty(menuB!.Categories);
        Assert.Empty(menuB.Products);
        Assert.Equal(HttpStatusCode.NotFound, productInForeignCategory.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, updateForeignProduct.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignGroup.StatusCode);
        Assert.Equal("menu.modifier_group_not_found", await AuthTests.ReadCodeAsync(foreignGroup));
        var menuA = await a.Admin.GetFromJsonAsync<MenuDto>("/api/menu", Json);
        Assert.Equal("Margherita", Assert.Single(menuA!.Products).Name);
    }

    [Fact]
    public async Task RemovingSizeStillPriced_Returns409()
    {
        var tenant = await factory.CreateCompanyAsync("menu");
        var (pizza, _, _) = await BuildPizzaMenuAsync(tenant.Admin);

        using var response = await tenant.Admin.PutAsJsonAsync($"/api/menu/categories/{pizza.Id}",
            new SaveCategoryRequest("Pizza", [new(pizza.Sizes[0].Id, "32 cm")]), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("menu.size_in_use", await AuthTests.ReadCodeAsync(response));
    }

    [Fact]
    public async Task UpdatingCategory_RenamesAndAddsSizesKeepingIds()
    {
        var tenant = await factory.CreateCompanyAsync("menu");
        var (pizza, _, _) = await BuildPizzaMenuAsync(tenant.Admin);

        using var response = await tenant.Admin.PutAsJsonAsync($"/api/menu/categories/{pizza.Id}",
            new SaveCategoryRequest("Pizze", [new(pizza.Sizes[0].Id, "30 cm"), new(pizza.Sizes[1].Id, "40 cm"), new(null, "50 cm")]), Json);
        var updated = await response.Content.ReadFromJsonAsync<CategoryDto>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["30 cm", "40 cm", "50 cm"], updated!.Sizes.Select(s => s.Name));
        Assert.Equal(pizza.Sizes[0].Id, updated.Sizes[0].Id);
    }

    [Fact]
    public async Task UpdatingProduct_AddsPriceForNewSizeAndModifierGroup()
    {
        var tenant = await factory.CreateCompanyAsync("menu");
        var (pizza, sauces, margherita) = await BuildPizzaMenuAsync(tenant.Admin);
        using var categoryResponse = await tenant.Admin.PutAsJsonAsync($"/api/menu/categories/{pizza.Id}",
            new SaveCategoryRequest("Pizza", [.. pizza.Sizes.Select(s => new SizeInput(s.Id, s.Name)), new(null, "50 cm")]), Json);
        var withXl = (await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>(Json))!;
        var xl = withXl.Sizes[2].Id;
        var toppings = await CreateAsync<ModifierGroupDto>(tenant.Admin, "/api/menu/modifier-groups",
            new SaveModifierGroupRequest(pizza.Id, "Dodatki", 0, 3,
                [new(null, "Ser", true, [.. withXl.Sizes.Select(s => new PriceDto(s.Id, 5m))])]));

        using var response = await tenant.Admin.PutAsJsonAsync($"/api/menu/products/{margherita.Id}",
            new SaveProductRequest(pizza.Id, "Margherita", null, true, true,
                [.. margherita.Prices, new(xl, 41m)], [sauces.Id, toppings.Id]), Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var menu = await tenant.Admin.GetFromJsonAsync<MenuDto>("/api/menu", Json);
        var saved = Assert.Single(menu!.Products);
        Assert.Equal(41m, saved.Prices.Single(p => p.SizeId == xl).Price);
        Assert.Equal([sauces.Id, toppings.Id], saved.ModifierGroupIds);
    }

    [Fact]
    public async Task InvalidPrice_Returns400WithFieldError()
    {
        var tenant = await factory.CreateCompanyAsync("menu");
        var pizza = await CreateAsync<CategoryDto>(tenant.Admin, "/api/menu/categories", new SaveCategoryRequest("Pizza", [new(null, "32 cm")]));

        using var response = await tenant.Admin.PostAsJsonAsync("/api/menu/products",
            new SaveProductRequest(pizza.Id, "Tania", null, false, true, [new(pizza.Sizes[0].Id, 10.555m)], []), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validation.price", json.RootElement.GetProperty("errors").GetProperty("prices[0].price")[0].GetString());
    }

    [Fact]
    public async Task Settings_ChangeHalfAndHalfRule()
    {
        var tenant = await factory.CreateCompanyAsync("menu");

        using var response = await tenant.Admin.PutAsJsonAsync("/api/menu/settings", new MenuSettingsDto(HalfAndHalfPricing.AverageOfHalves), Json);
        var menu = await tenant.Admin.GetFromJsonAsync<MenuDto>("/api/menu", Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HalfAndHalfPricing.AverageOfHalves, menu!.HalfAndHalfPricing);
    }

    [Fact]
    public async Task ModifierGroup_Update_KeepsModifierIds()
    {
        var tenant = await factory.CreateCompanyAsync("menu");
        var (pizza, sauces, _) = await BuildPizzaMenuAsync(tenant.Admin);
        var garlic = sauces.Modifiers[0];

        using var response = await tenant.Admin.PutAsJsonAsync($"/api/menu/modifier-groups/{sauces.Id}",
            new SaveModifierGroupRequest(pizza.Id, "Sosy", 0, 2,
            [
                new(garlic.Id, "Czosnkowy", false, garlic.Prices),
                new(null, "BBQ", true, [new(pizza.Sizes[0].Id, 3m), new(pizza.Sizes[1].Id, 4m)]),
            ]), Json);
        var updated = await response.Content.ReadFromJsonAsync<ModifierGroupDto>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(garlic.Id, updated!.Modifiers[0].Id);
        Assert.False(updated.Modifiers[0].IsActive);
        Assert.Equal(["Czosnkowy", "BBQ"], updated.Modifiers.Select(m => m.Name));
    }

    private static async Task<(CategoryDto Pizza, ModifierGroupDto Sauces, ProductDto Margherita)> BuildPizzaMenuAsync(HttpClient admin)
    {
        var pizza = await CreateAsync<CategoryDto>(admin, "/api/menu/categories",
            new SaveCategoryRequest("Pizza", [new(null, "32 cm"), new(null, "42 cm")]));
        var (small, large) = (pizza.Sizes[0].Id, pizza.Sizes[1].Id);

        var sauces = await CreateAsync<ModifierGroupDto>(admin, "/api/menu/modifier-groups",
            new SaveModifierGroupRequest(pizza.Id, "Sos", 1, 1,
            [
                new(null, "Czosnkowy", true, [new(small, 2m), new(large, 2m)]),
                new(null, "Pomidorowy", true, [new(small, 2m), new(large, 3m)]),
            ]));

        var margherita = await CreateAsync<ProductDto>(admin, "/api/menu/products",
            new SaveProductRequest(pizza.Id, "Margherita", "Sos, ser", true, true, [new(small, 25.5m), new(large, 32m)], [sauces.Id]));

        return (pizza, sauces, margherita);
    }

    private static async Task<T> CreateAsync<T>(HttpClient client, string url, object body)
    {
        using var response = await client.PostAsJsonAsync(url, body, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
}
