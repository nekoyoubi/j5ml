using System.Text.Json;
using System.Text.Json.Nodes;

using J5ml;

using Xunit;

namespace J5mlTests;

/// <summary>
/// Asserts this implementation against the shared corpus.
///
/// The corpus is authoritative. A case that fails here is either a bug in this
/// implementation or a change to the format that every other implementation in
/// the repository must also make.
/// </summary>
public class ConformanceTests
{
	private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, JsonNode>> Corpus = LoadCorpus();

	private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, JsonNode>> LoadCorpus()
	{
		var path = Path.Combine(AppContext.BaseDirectory, "conformance-tests.json");
		var root = JsonNode.Parse(File.ReadAllText(path))
			?? throw new InvalidOperationException("the conformance corpus must be valid JSON");

		return new[] { "cases", "authoring", "invalid" }.ToDictionary(
			tier => tier,
			tier => (IReadOnlyDictionary<string, JsonNode>)root[tier]!.AsArray()
				.ToDictionary(entry => entry!["name"]!.GetValue<string>(), entry => entry!));
	}

	public static TheoryData<string> Cases => Names("cases");
	public static TheoryData<string> Authoring => Names("authoring");
	public static TheoryData<string> Invalid => Names("invalid");

	private static TheoryData<string> Names(string tier)
	{
		var data = new TheoryData<string>();
		foreach (var name in Corpus[tier].Keys) data.Add(name);
		return data;
	}

	private static JsonNode Entry(string tier, string name) => Corpus[tier][name];

	/// <summary>
	/// Rebuilds a node from the corpus's neutral tree encoding so the comparison is
	/// against the corpus rather than against this implementation's own parse.
	/// </summary>
	private static Node ExpectedNode(JsonNode spec)
	{
		if (spec["text"] is { } text) return new Text(text.GetValue<string>());

		var name = spec["name"]!.GetValue<string>();

		var attrs = new Dictionary<string, JsonNode?>();
		if (spec["attrs"] is JsonObject map)
		{
			foreach (var pair in map) attrs[pair.Key] = pair.Value?.DeepClone();
		}

		var children = new List<Node>();
		if (spec["children"] is JsonArray list)
		{
			foreach (var child in list) children.Add(ExpectedNode(child!));
		}

		return new Element(name, attrs, children);
	}

	private static void AssertSameNode(Node expected, Node actual)
	{
		switch (expected)
		{
			case Text expectedText:
				var actualText = Assert.IsType<Text>(actual);
				Assert.Equal(expectedText.Value, actualText.Value);
				return;

			case Element expectedElement:
				var actualElement = Assert.IsType<Element>(actual);
				Assert.Equal(expectedElement.Name, actualElement.Name);

				Assert.Equal(
					expectedElement.Attrs.Keys.OrderBy(key => key, StringComparer.Ordinal),
					actualElement.Attrs.Keys.OrderBy(key => key, StringComparer.Ordinal));

				foreach (var pair in expectedElement.Attrs)
				{
					var actualValue = actualElement.Attrs[pair.Key];
					Assert.Equal(
						pair.Value?.ToJsonString() ?? "null",
						actualValue?.ToJsonString() ?? "null");
				}

				Assert.Equal(expectedElement.Children.Count, actualElement.Children.Count);
				for (var index = 0; index < expectedElement.Children.Count; index++)
				{
					AssertSameNode(expectedElement.Children[index], actualElement.Children[index]);
				}
				return;

			default:
				throw new InvalidOperationException("unreachable node kind");
		}
	}

	[Theory]
	[MemberData(nameof(Cases))]
	public void ParsesEveryCanonicalCaseToTheExpectedTree(string name)
	{
		var entry = Entry("cases", name);
		var node = J5mlDocument.Parse(entry["json"]!.GetValue<string>());
		AssertSameNode(ExpectedNode(entry["tree"]!), node);
	}

	[Theory]
	[MemberData(nameof(Cases))]
	public void SerializesEveryRoundTrippingCaseByteExact(string name)
	{
		var entry = Entry("cases", name);
		if (entry["roundTrips"]?.GetValue<bool>() is false) return;

		var json = entry["json"]!.GetValue<string>();
		Assert.Equal(json, J5mlDocument.Stringify(J5mlDocument.Parse(json)));
	}

	[Theory]
	[MemberData(nameof(Cases))]
	public void ParsesEveryCanonicalCaseInStrictJsonMode(string name)
	{
		var entry = Entry("cases", name);
		var node = J5mlDocument.ParseJson(entry["json"]!.GetValue<string>());
		AssertSameNode(ExpectedNode(entry["tree"]!), node);
	}

	[Theory]
	[MemberData(nameof(Authoring))]
	public void CanonicalizesEveryAuthoringCase(string name)
	{
		var entry = Entry("authoring", name);
		var node = J5mlDocument.Parse(entry["json5"]!.GetValue<string>());
		Assert.Equal(entry["serializesTo"]!.GetValue<string>(), J5mlDocument.Stringify(node));
	}

	[Theory]
	[MemberData(nameof(Authoring))]
	public void RejectsEveryAuthoringCaseInStrictJsonMode(string name)
	{
		var entry = Entry("authoring", name);
		var json5 = entry["json5"]!.GetValue<string>();

		// An authoring sample that is already valid JSON carries no JSON5 syntax to
		// reject, so strict mode is right to accept it.
		var isAlreadyJson = true;
		try { JsonNode.Parse(json5); }
		catch (JsonException) { isAlreadyJson = false; }

		if (isAlreadyJson) return;

		Assert.Throws<J5mlException>(() => J5mlDocument.ParseJson(json5));
	}

	[Theory]
	[MemberData(nameof(Invalid))]
	public void RejectsEveryInvalidCase(string name)
	{
		var entry = Entry("invalid", name);
		Assert.Throws<J5mlException>(() => J5mlDocument.Parse(entry["json"]!.GetValue<string>()));
	}
}
