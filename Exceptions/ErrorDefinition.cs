namespace MyFirstApi.Exceptions;

// One entry of the error catalog: a stable code the frontend can switch on,
// the HTTP status, and message templates in English and Thai. Templates use
// {name} placeholders filled from the error's args.
public record ErrorDefinition(string Code, int Status, string En, string Th);

// Which entry of a batch request an error belongs to ("Item 2: ...", "Leg 1: ...").
public record ErrorScope(string Kind, int Number)
{
    public static ErrorScope Item(int number) => new("item", number);
    public static ErrorScope Leg(int number) => new("leg", number);

    public string EnPrefix => Kind == "leg" ? $"Leg {Number}: " : $"Item {Number}: ";
    public string ThPrefix => Kind == "leg" ? $"ช่วงที่ {Number}: " : $"รายการที่ {Number}: ";
}
