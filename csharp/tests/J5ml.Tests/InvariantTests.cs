using System.Text.Json.Nodes;

using J5ml;

using Xunit;

namespace J5mlTests;

/// <summary>
/// Behaviour this implementation owns that the corpus does not describe: the
/// traversal surface, construction, and the error type. Format behaviour belongs
/// in the corpus, not here.
/// </summary>
public class InvariantTests
{
	[Fact]
	public void WalksParentsBeforeChildren()
	{
		var tree = J5mlDocument.Parse("""["a",["b"],["c",["d"]]]""");
		var seen = new List<string>();

		J5mlDocument.Traverse(tree, (node, _) =>
		{
			if (node is Element element) seen.Add(element.Name);
			return Walk.Continue;
		});

		Assert.Equal(new[] { "a", "b", "c", "d" }, seen);
	}

	[Fact]
	public void ReportsThePathOfEachNode()
	{
		var tree = J5mlDocument.Parse("""["doc",{},["script",{},"x"]]""");
		string? found = null;

		J5mlDocument.Traverse(tree, (node, path) =>
		{
			if (node is Element { Name: "script" })
			{
				found = J5mlDocument.PathToString(path);
				return Walk.Stop;
			}
			return Walk.Continue;
		});

		Assert.Equal("/0", found);
	}

	[Fact]
	public void StopEndsTheTraversal()
	{
		var tree = J5mlDocument.Parse("""["a",["b"],["c"]]""");
		var seen = new List<string>();

		J5mlDocument.Traverse(tree, (node, _) =>
		{
			if (node is Element element) seen.Add(element.Name);
			return IsNamed(node, "b") ? Walk.Stop : Walk.Continue;
		});

		Assert.Equal(new[] { "a", "b" }, seen);
	}

	[Fact]
	public void SkipLeavesChildrenUnvisitedButContinuesWithSiblings()
	{
		var tree = J5mlDocument.Parse("""["a",["b",["deep"]],["c"]]""");
		var seen = new List<string>();

		J5mlDocument.Traverse(tree, (node, _) =>
		{
			if (node is Element element) seen.Add(element.Name);
			return IsNamed(node, "b") ? Walk.Skip : Walk.Continue;
		});

		Assert.Equal(new[] { "a", "b", "c" }, seen);
	}

	[Fact]
	public void APathHandedToTheVisitorSurvivesTheTraversal()
	{
		var tree = J5mlDocument.Parse("""["a",["b"],["c"]]""");
		var kept = new List<IReadOnlyList<int>>();

		J5mlDocument.Traverse(tree, (_, path) =>
		{
			kept.Add(path);
			return Walk.Continue;
		});

		Assert.Equal(new[] { "", "/0", "/1" }, kept.Select(J5mlDocument.PathToString));
	}

	/// <summary>
	/// U+1F600 is above U+FFFF, so a UTF-16 code-unit sort would place it before
	/// U+FFFD and disagree with the byte order every other implementation uses.
	/// </summary>
	[Fact]
	public void OrdersAttributeNamesByCodePointRatherThanCodeUnit()
	{
		var attrs = new Dictionary<string, JsonNode?>
		{
			["\U0001F600"] = JsonValue.Create(1),
			["�"] = JsonValue.Create(2),
		};

		// U+FFFD (65533) sorts before U+1F600 (128512) by code point. Sorting by
		// UTF-16 code unit would compare the leading surrogate D83D (55357) instead
		// and put the emoji first, which is the bug this pins.
		var expected = "[\"x\",{\"�\":2,\"\U0001F600\":1}]";

		Assert.Equal(expected, J5mlDocument.Stringify(new Element("x", attrs)));
	}

	[Fact]
	public void OrdersNamesInsideAnAttributeValueToo()
	{
		var inner = new JsonObject { ["b"] = JsonValue.Create(1), ["a"] = JsonValue.Create(2) };
		var attrs = new Dictionary<string, JsonNode?> { ["style"] = inner };

		Assert.Equal(
			"""["x",{"style":{"a":2,"b":1}}]""",
			J5mlDocument.Stringify(new Element("x", attrs)));
	}

	[Fact]
	public void RefusesToSerializeAnEmptyName()
	{
		Assert.Throws<J5mlException>(() => J5mlDocument.Stringify(new Element("")));
	}

	[Fact]
	public void ParseFailuresCarryTheJ5mlExceptionType()
	{
		Assert.Throws<J5mlException>(() => J5mlDocument.Parse("{"));
		Assert.Throws<J5mlException>(() => J5mlDocument.Parse("42"));
	}

	[Fact]
	public void BuildsAnElementWithoutAttributesOrChildren()
	{
		Assert.Equal("""["div"]""", J5mlDocument.Stringify(new Element("div")));
	}

	[Fact]
	public void TheActionOverloadWalksTheWholeTree()
	{
		var tree = J5mlDocument.Parse("""["a",["b",["deep"]],["c"]]""");
		var seen = new List<string>();

		J5mlDocument.Traverse(tree, (node, _) =>
		{
			if (node is Element element) seen.Add(element.Name);
		});

		Assert.Equal(new[] { "a", "b", "deep", "c" }, seen);
	}

	private static bool IsNamed(Node node, string name) =>
		node is Element element && element.Name == name;
}
