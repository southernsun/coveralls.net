﻿using System;
using System.IO;
using System.Net.Http;
using System.Xml.Linq;
using CommandLine;
using Coveralls.Lib;
using Newtonsoft.Json;

namespace coveralls.net
{
    public enum ParserTypes
    {
        OpenCover,
    }

    public class LocalFileSystem : IFileSystem
    {
        public string ReadFileText(string path)
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
    }

    public class Program
    {
        static void Main(string[] args)
        {
            ICoverageParser parser;
            var fileSystem = new LocalFileSystem();
            var opts = Parser.Default.ParseArguments<CommandLineOptions>(args).Value;

            switch (opts.Parser)
            {
                case ParserTypes.OpenCover:
                    parser = new OpenCoverParser(fileSystem);
                    break;
                default:
                    parser = new OpenCoverParser(fileSystem);
                    break;
            }
            parser.Report = XDocument.Parse(fileSystem.ReadFileText(opts.InputFile));

            var coverageFiles = parser.Generate();

            // Job and Commit Data
            var gitData = new GitData
            {
                Branch = Environment.GetEnvironmentVariable(""),
                Head = new CommitData
                {
                    Id = Environment.GetEnvironmentVariable("APPVEYOR_REPO_COMMIT"),
                    Message = Environment.GetEnvironmentVariable("APPVEYOR_REPO_COMMIT_MESSAGE"),
                    Author = Environment.GetEnvironmentVariable("APPVEYOR_REPO_COMMIT_AUTHOR"),
                    AuthorEmail = Environment.GetEnvironmentVariable("APPVEYOR_REPO_COMMIT_AUTHOREMAIL"),
                }
            };

            var coverallsData = new CoverallsData
            {
                ServiceName = "appveyor",
                ServiceJobId = Environment.GetEnvironmentVariable("APPVEYOR_JOB_ID") ?? "0",
                RepoToken = "UCIcRAOyPJIDrjvG8MreBKnKPonmR2L10",
                SourceFiles = coverageFiles.ToArray(),
                Git = gitData
            };

            // Send to coveralls.io
            if (!Send(coverallsData))
            {
                Console.Error.WriteLine("Coveralls - Send Failed");
                Environment.Exit(1);
            }

            Environment.Exit(0);
        }

        private static bool Send(CoverallsData data)
        {
            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json);

            using (var client = new HttpClient())
            {
                using (var formData = new MultipartFormDataContent())
                {
                    formData.Add(content, "json_file", "coverage.json");
                    var response = client.PostAsync(@"https://coveralls.io/api/v1/jobs", formData);
                    return response.Result.IsSuccessStatusCode;
                }
            }
        }
    }

    internal class CommandLineOptions
    {
        [Value(0)]
        public string InputFile { get; set; }

        [Option('p', "parser", HelpText = "Parser to use (Currently only supports OpenCover)")]
        public ParserTypes Parser { get; set; }

        private bool _useOpenCover;
        [Option("opencover")]
        public bool UseOpenCover
        {
            get { return _useOpenCover; }
            set
            {
                _useOpenCover = value;
                if (_useOpenCover) Parser = ParserTypes.OpenCover;
            }
        }
    }
}