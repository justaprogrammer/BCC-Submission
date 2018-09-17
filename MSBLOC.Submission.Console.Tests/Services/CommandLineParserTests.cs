using Bogus;
using FluentAssertions;
using MSBLOC.Submission.Console.Services;
using NSubstitute;
using Xunit;

namespace MSBLOC.Submission.Console.Tests.Services
{
    public class CommandLineParserTests
    {
        private static readonly Faker Faker;

        static CommandLineParserTests()
        {
            Faker = new Faker();
        }

        [Fact]
        public void ShouldCallForHelpIfNothingSent()
        {
            var listener = Substitute.For<ICommandLineParserCallBackListener>();
            var commandLineParser = new CommandLineParser(listener.Callback);
            var applicationArguments = commandLineParser.Parse(new string[0]);

            applicationArguments.Should().BeNull();

            listener.Received(1).Callback(Arg.Any<string>());
        }

        [Fact]
        public void ShouldParseForHelpIfNothingSent()
        {
            var listener = Substitute.For<ICommandLineParserCallBackListener>();
            var commandLineParser = new CommandLineParser(listener.Callback);
            var applicationArguments = commandLineParser.Parse(new []{"-?"});

            applicationArguments.Should().BeNull();

            listener.Received(1).Callback(Arg.Any<string>());
        }

        [Fact]
        public void ShouldRequireRequiredArguments()
        {
            var listener = Substitute.For<ICommandLineParserCallBackListener>();
            var commandLineParser = new CommandLineParser(listener.Callback);

            var inputPath = Faker.System.FilePath();
            var token = Faker.Random.String();
            var applicationArguments = commandLineParser.Parse(new[]{"-i", $@"""{inputPath}""", "-t", $@"""{token}"""});

            listener.DidNotReceive().Callback(Arg.Any<string>());

            applicationArguments.Should().NotBeNull();
            applicationArguments.InputFile.Should().Be(inputPath);
            applicationArguments.Token.Should().Be(token);

            applicationArguments = commandLineParser.Parse(new[]{"--input", $@"""{inputPath}""", "--token", $@"""{token}"""});

            listener.DidNotReceive().Callback(Arg.Any<string>());

            applicationArguments.Should().NotBeNull();
            applicationArguments.InputFile.Should().Be(inputPath);
            applicationArguments.Token.Should().Be(token);
        }

        public interface ICommandLineParserCallBackListener
        {
            void Callback(string obj);
        }
    }
}