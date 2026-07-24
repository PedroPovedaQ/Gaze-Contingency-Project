import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname } from "node:path";
import {
  generatePodcastFromText,
  PodcastPipelineError,
} from "/Users/pedro.poveda/Documents/code/mj/webkasa_app/apps/web-kasa-frontend/lib/tools/podcast-pipeline";

const sourcePath =
  "/Users/pedro.poveda/Gaze Contingency Project/docs/podcast_output/gaze_contingency_long_podcast_script.md";
const outputPath =
  "/Users/pedro.poveda/Gaze Contingency Project/docs/podcast_output/gaze-contingency-long-podcast.wav";

async function main() {
  const sourceText = readFileSync(sourcePath, "utf8").slice(0, 29_000);

  const podcast = await generatePodcastFromText({
    sourceText,
    title: "Seeing the System: Gaze-Contingent AI Assistance in Mixed Reality Search",
    style: "intellectual",
    tone: "casual",
    length: "long",
    segmentCount: 20,
    hostCount: 2,
    hostVoiceIds: ["alloy", "echo"],
    extraPrompt:
      "Create a long, polished, two-host technical explainer podcast. Preserve the concrete file names, experiment flow, flowchart structure, and research implications from the source. Do not compress into a brief summary. Make it sound like a thoughtful conversational walkthrough for a CAP 6117 project presentation.",
    includeSegmentAudio: false,
  });

  mkdirSync(dirname(outputPath), { recursive: true });
  writeFileSync(outputPath, Buffer.from(podcast.audio.base64, "base64"));
  writeFileSync(
    `${outputPath}.json`,
    JSON.stringify(
      {
        title: podcast.title,
        description: podcast.description,
        durationSeconds: podcast.audio.durationSeconds,
        sampleRate: podcast.audio.sampleRate,
        voices: podcast.voices,
        segments: podcast.segments.map((segment) => ({
          index: segment.index,
          speaker: segment.speaker,
          voiceId: segment.voiceId,
          text: segment.text,
          startTime: segment.startTime,
          endTime: segment.endTime,
        })),
      },
      null,
      2,
    ),
  );
  writeFileSync(`${outputPath}.script.txt`, podcast.scriptText);

  console.log(
    JSON.stringify(
      {
        outputPath,
        metadataPath: `${outputPath}.json`,
        scriptPath: `${outputPath}.script.txt`,
        durationSeconds: podcast.audio.durationSeconds,
        segments: podcast.segments.length,
      },
      null,
      2,
    ),
  );
}

main().catch((error) => {
  if (error instanceof PodcastPipelineError) {
    console.error(`${error.code}: ${error.message}`);
    process.exit(1);
  }
  console.error(error);
  process.exit(1);
});
