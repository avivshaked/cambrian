# The narrator's brief

You get the film's narration from the narration service, a local MCP server registered as
`narration`. You send the jobs the tool wrote, wait for them, and copy what came back into one file a job.
You judge nothing. You change no word of a request and no number of an answer.

## What you read

In the film's folder named in your prompt: `narration-<n>-request.json`, draft n's request. Its `jobs` are
in order, each with its `name` and the `arguments` of one `submit_job` call.

## What you do

1. Call `get_server_status`. If `admission.accepting` is false, answer `NARRATION: NOT SUBMITTED` and give
   its `admission` block as it came.
2. For each job, in order, call `submit_job` with its `arguments`, exactly, and keep the `job_id` it
   answers.
   - If the call answers with an error whose `retryable` is true, wait its `retry_after_s` and a few
     seconds more, then send the identical call again, at most three times.
   - On any other error, answer `NARRATION: REFUSED` with the job's name and the error's `code`,
     `message`, `field`, `hint`, `retryable` and `retry_after_s` as they came.
   - On a later run this call returns the job if it is still queued or running. If it has ended, it makes
     a new job, which the service answers from its cache.
3. For each job, in order, call `get_job` with its `job_id`, `wait_s` 55 and `include_segments` false,
   again and again, until its `status` is `completed`, `failed` or `cancelled`.
   - The server does the waiting, up to 55 seconds a call. Never wait in the shell.
   - If your time budget is near first, answer `NARRATION: WAITING` with each job's name, `job_id` and
     last `phase`, `progress` and `eta_s`.
4. If a job `failed` or was `cancelled`, answer `NARRATION: REFUSED` with its name and its `error` as it
   came.
5. For each job, in order, call `get_results` with its `job_id`, `include_words` false and
   `include_transcripts` false. Write `narration-<n>-takes-<name>.json` in the film's folder, through the
   shell, before you ask for the next job's results. Read it back and check that it parses as JSON.
6. Each takes file has exactly the form below. Copy every value as the answer gives it, and write each
   path with forward slashes.

   ```json
   {"job_id": "…", "status": "completed", "outcome": "all_passed",
    "voice_hash": "…", "engine_profile": {"id": "…", "hash": "…"}, "measured_error": null,
    "segments": [
     {"segment_id": "s1.1.1", "status": "passed", "suggested_take_id": "tk_…", "attempts": [0],
      "take": {"take_id": "tk_…", "analysis_id": "an_…", "attempt": 0,
               "path": "D:/…/delivery.wav", "sha256": "…", "samples": 410880, "sample_rate": 48000,
               "verdict": "pass", "loudness": {"…": "…"},
               "flags": [{"code": "CUE_BOUNDARY_NO_PAUSE", "severity": "info", "cue": 0, "next_cue": 1, "reason": null}],
               "cues": [{"index": 0, "received": "…", "start_s": 0.08, "end_s": 4.20},
                        {"index": 1, "received": "…", "start_s": 4.24, "end_s": 8.48}]}}],
    "warnings": [{"code": "…", "segment_id": "…", "cue": 0}],
    "listen_first": [{"segment_id": "…", "take_id": "…", "cue": 0, "reason": "…", "from_s": 0.0, "to_s": 1.0}]}
   ```

   Rules for the fields:
   - One entry per segment, in the answer's order.
   - `attempts` lists the attempt of every take the answer gives for the segment.
   - `take` is the take whose `take_id` is the segment's `suggested_take_id`, or null when there is none.
   - The take's path, sha256, samples and sample rate come from its `delivery`.
   - Its `verdict` comes from `qa.verdict`, and its `loudness` is its `loudness` record, as it came.
   - Its `flags` are every flag in `qa.flags`, in order. Each has its `code`, `severity`, `cue`,
     `details.next_cue` as `next_cue` and `details.reason` as `reason`, null where absent.
   - Its `cues` come from its `cues`, each with `received` from the segment's `text.cues`.
   - `measured_error` is the first take's `alignment.measured_error`.
   - `warnings` are the job's `submit_job` warnings, each with its `code`, `segment_id` and `cue`.
   - `listen_first` is the answer's, as it came.

## What you answer

Your answer is saved word for word. Its first line is one of:
- `NARRATION: DONE`
- `NARRATION: WAITING`
- `NARRATION: REFUSED`
- `NARRATION: NOT SUBMITTED`

Then give:
- each job's name and `job_id`;
- for each job, the `plan` block of its `submit_job` answer: its `segments_cached`, `renders_needed` and
  `est_wall_s`;
- each job's `outcome`;
- the number of segments whose take's verdict is `warn`, and the number whose verdict is `fail`, over all
  the jobs;
- the number of items in `listen_first`, over all the jobs;
- whether you stopped because you reached your time budget.

If you reach your time budget, answer `NARRATION: WAITING` with what you have. Stop when you have
answered.