# Prose that reads as written by a skilled human: research for narrated science videos

Compiled on 2026-09-26 from web sources only, for the narration and on-screen text of short narrated videos for social media, each telling what happened in one experiment of an artificial-life simulator. No file in the repository was read to write it. It is meant to be used later without going back to the sources, so each source is described in full in Part 2: who made it, what it studied and how, what it found, what it says in its own words, where it is weak, and what it means for our prose.

## How to read this document

Each claim carries a label for the kind of source behind it, and a URL.

**[Study]** is a paper that reports data: a peer-reviewed article, or a preprint, which is said as such. **[Measurement]** is a corpus count or a feature analysis, usually part of a study. **[Practitioner]** is advice from writers, editors, broadcasters or teachers, which rests on their experience and not on data. **[Vendor]** is documentation from a model's maker, which rests on the maker's internal testing, none of it published. **[Field guide]** is Wikipedia's community-maintained list of signs, built from cases its editors found. **[Essay]** is an opinion piece by a named writer. **[News]** is a news report or a public statement.

The sources were read through a fetching tool that hands back a model's summary of the page, not the page itself, and some were seen only as an abstract, a search-result snippet or a secondary write-up. The source table at the end (Part 7) says which. Numbers here were copied from those summaries; check any number against the paper before quoting it in public. Several publisher pages refused the tool (Science, Nature, PNAS, SAGE, Wiley, Taylor and Francis), and some details are missing for that reason; the catalogue says so where it matters.

A statement that is my own reading, not a source's finding, is marked **(inference)**.

The document has seven parts. Part 1 answers the five questions asked, in prose, with the evidence behind each answer. Part 2 is the source catalogue. Part 3 lists the tells to avoid. Part 4 gives the ten best-supported rules for our kind of prose. Part 5 describes a working process. Part 6 lists what I looked for and did not find. Part 7 is the source table.

---

# Part 1. The five questions

## 1.1 The tells readers and listeners notice

The best evidence on what readers actually notice comes from Russell, Karpinska and Iyyer (2025), who asked people to explain, in a paragraph, why they judged an article to be AI-written. Among annotators who use LLMs heavily and who were right nearly every time, the clues they gave were, in order: vocabulary (in 53.1% of explanations), sentence structure such as "not only ... but also" (35.9%), grammar and punctuation that is too perfect (24.8%), a lack of originality or surprise (23.7%), quotations that sound like the text around them (22.3%), over-explanation (19.5%), too-consistent formatting (15.0%), tidy optimistic conclusions (13.1%), excessive formality and no contractions (12.3%), recurring names and titles such as "Sarah Thompson" or "Dr." (11.7%), a uniformly neutral or positive tone (9.3%), generic introductions (7.3%) and factual slips (7.2%). [Study] https://arxiv.org/abs/2501.15654 The same headings organise what follows.

**Words.** The words that give AI prose away are style words, not content words: mostly verbs and adjectives that appraise or connect rather than name things. In 15 million biomedical abstracts, two thirds of the words whose frequency jumped after ChatGPT were verbs and one in seven adjectives (Kobak et al., [Study]); the biggest jumps were *delves*, *underscores* and *showcasing*. GPT-4o uses *camaraderie*, *tapestry*, *intricate*, *underscore* and *amidst* over a hundred times as often as human writers do (Reinhart et al., [Study]). Wikipedia's field guide keeps dated lists by model generation (for GPT-5: *emphasizing, enhance, highlighting, showcasing*) and a list of promotional words (*boasts, vibrant, rich, profound, nestled, in the heart of, groundbreaking, renowned, diverse array*) [Field guide]. In fiction the tells are cliches of sensation (*voice barely above a whisper*, *heart hammered*), stock names (*Elara*, *Kael*) and intensifiers (*profound sense*), some of them more than a thousand times their human frequency (Antislop, [Study]). Kriss's essay adds the vocabulary of fake subtlety: everything *quiet*, *shadowy*, *subtle*, *woven* [Essay]. The word tells change with each model generation, and people are adopting them in speech (Yakura et al., [Study]), so one such word proves nothing; many together are a strong sign (Wikipedia's caveat).

**Sentence patterns.** Five patterns recur across the sources. First, the corrective figure "it's not X, it's Y" (epanorthosis), which Antislop measured at 6.3 times the human rate in some models and a small study on Claude models found at 2.2 times the human rate in speeches specifically [Study, preprints]. Second, the rule of three: "adjective, adjective, adjective" or "short phrase, short phrase, and short phrase" [Field guide; Essay]; I found no count of it. Third, the editorial "-ing" tail: a present participle clause that closes a sentence by asserting what it means (", highlighting the importance of ...", ", reflecting ..."); GPT-4o uses present participial clauses at 5.3 times the human rate (Reinhart et al., [Study]) and Wikipedia calls the result "superficial analysis" [Field guide]. Fourth, avoiding plain "is": *serves as, stands as, marks, represents, boasts, features* [Field guide]. Fifth, abstraction: nominalisations at about twice the human rate and phrasal coordination (noun phrases joined by "and") at about twice (Reinhart; Herbold et al. [Study]). Beneath these sit repeated part-of-speech templates, most of which the models learned in pre-training and which alignment does not remove (Shaib et al. 2024, [Study]).

**Structure.** AI prose explains too much and wraps up too neatly. Readers notice the summary at the end (Russell et al.), and the "despite these challenges" or "future outlook" conclusion (Wikipedia). In stories, AI "over-explain[s] themes and favor[s] tidy, single-track plots", with less moral ambiguity and less play with time than human writers, and Claude's stories escalate flatly (StoryScope, [Study]). When professional writers edited LLM paragraphs, nearly a fifth of their edits cut unnecessary or redundant exposition (LAMP, [Study]).

**Tone.** The tone is even, positive and inflated. It claims significance ("a testament to", "a pivotal role", "setting the stage for") where a human would state the fact, which Wikipedia explains as the model regressing to the mean and replacing specific facts with generic praise [Field guide]. Human news carries more fear and disgust and less joy than LLM news (Munoz-Ortiz et al., [Study]). Hedging is model-specific: GPT-4o uses downtoners more than humans, Llama less (Reinhart). Vague attribution ("experts argue", "researchers") is a tell too (Wikipedia; Desaire et al. [Study]).

**Punctuation and formatting.** The em dash is the best-known punctuation tell of recent models: GPT-4.1 at about 10.6 per thousand words and Claude Opus 4.6 at about 9.1, against about 3.2 in a human control (Freeburg, [Measurement, preprint]). It is new: in 2023 human scientists used more dashes than GPT-3.5 did (Desaire et al.). Bold, headings, lists with inline headers, title case and emoji as formatting are tells on a page [Field guide]; they are inaudible in narration but show in on-screen text.

## 1.2 What measurements say, and how well people tell the two apart

**Frequencies.** Word-frequency studies are the strongest evidence, because they are large and need no judgment: 15 million abstracts in Kobak et al.; the 21 focal words of Juzek and Ward; hundreds of thousands of podcast episodes in Yakura et al. They show a small set of style words rising abruptly after late 2022, spreading into human speech, and probably amplified by preference training, although the cause is not settled [Study].

**Grammar.** Reinhart et al. compared human texts with model continuations of the same texts on Biber's 66 grammatical and rhetorical features and found that instruction-tuned models differ from humans far more than base models do: "Instruction tuning pushes models to produce text that reads *unlike* a human" [Study]. A classifier on those features told human text from each model at 93 to 98% accuracy. Herbold et al. found the same kind of difference in essays: more nominalisations, more complex sentences, fewer discourse and epistemic markers in ChatGPT's essays, and more modal and epistemic constructions (the writer's own stance) in the students' [Study].

**Rhythm.** The measured rhythm difference is sentence-length variance. Desaire et al. found that human scientists vary sentence length more than ChatGPT (GPT-3.5) did, and write more very short sentences (under 11 words) and more very long ones (over 34) [Study]. Munoz-Ortiz et al. found the same in news: human sentence lengths are "more scattered" [Study]. This is the measured basis of what commercial detectors call "burstiness".

**Narrative.** LLM stories pass 3 to 10 times fewer of 14 expert creativity tests than professional stories (Chakrabarty et al. 2024 [Study]); they cluster together in narrative space while human stories spread out (StoryScope [Study]); and writers who take ideas from an LLM write stories that are individually better but collectively more alike (Doshi and Hauser [Study]).

**Why.** Three lines of evidence point at alignment (instruction tuning and preference training) as the main source of the house style, with pre-training supplying the templates. Base models match human feature rates more closely than their instruction-tuned versions (Reinhart et al.) and are better at randomness and creative writing (West and Potts [Study]). Annotators who rate outputs prefer familiar text, so preference training narrows the model to typical answers ("typicality bias", Zhang et al. [Study, preprint]). And the lexical tells rise in ways consistent with preference training, though the direct test was inconclusive (Juzek and Ward [Study]). The em dash may be different: Freeburg finds it in base models already and ties it to markdown in the training data [Measurement, preprint].

**Human detection.** Untrained people are at chance. Clark et al. found evaluators at chance on GPT-3 text, and training raised them to 55% at best [Study]. People use heuristics that are wrong, taking first-person pronouns, contractions and family topics as signs of a human writer (Jakesch et al., 4,600 participants [Study]). Non-experts preferred AI imitations of famous poets to the poets themselves and identified them below chance, at 46.6% (Porter and Machery [Study]). But people who use LLMs heavily for writing are very good at it: 92.7% of AI articles caught with 3.3% false alarms, and a majority of five such readers wrong on 1 article in 300, which beats most commercial detectors. Paraphrasing did not fool them, nor did a model instructed with a guidebook built from their own clues (Russell et al. [Study]). Wikipedia's guide sums up the research the same way: most humans are at chance, heavy LLM users reach about 90% [Field guide]. For speech, listeners caught synthetic voices 73% of the time (Mai et al. [Study]); that concerns the voice, not the script. And labelling news as AI-written lowers trust in the article and in its publisher, even when readers find it no less accurate (Toff and Simon [Study]).

What this means for us (inference): most viewers of a short video will not reliably notice AI prose, but a growing minority who use LLMs daily will, quickly, and a viewer who suspects it trusts the video less. The tells are also, separately, what makes prose worse, which 1.3 and the LAMP study show, so they are worth removing whatever the audience.

## 1.3 What skilled human writing does instead

The advice of writers and editors converges on six things, and for most of them there is now some evidence from the studies above that the LLM default does the opposite.

**It names particulars.** Strunk's rule 16, "Use definite, specific, concrete language", and its gloss, "Prefer the specific to the general, the definite to the vague, the concrete to the abstract", are the oldest statement [Practitioner]. Ted Chiang's version is that a model makes "an average of the choices that other writers have made ... equivalent to the least interesting choices possible", while art "requires making choices at every scale" [Essay]. The evidence: professional editors of LLM paragraphs kept fixing "lack of specificity and detail" at a steady rate across quality levels, even in paragraphs where the cliches were already gone (LAMP [Study]); Wikipedia's editors see models replacing specific facts with generic praise [Field guide]; Russell's experts cited lack of originality or surprise in almost a quarter of their explanations [Study].

**It cuts.** Orwell: never use a long word where a short one will do; if it is possible to cut a word out, cut it out; never use the passive where you can use the active; never use a figure of speech you are used to seeing in print [Practitioner]. Zinsser: strip "every word that serves no function, every long word that could be a short word, every adverb that carries the same meaning that's already in the verb" [Practitioner]. The evidence: writers' edits of LLM text were 74% replacements and 18% deletions (LAMP [Study]).

**It varies its rhythm.** Provost's passage ("This sentence has five words. ... I vary the sentence length, and I create music.") is the practitioner's statement [Practitioner]; Desaire's and Munoz-Ortiz's variance findings are the measurement [Study].

**It has a writer who has seen something.** Pinker's "classic style" treats prose as "a window onto the world": the writer has seen something and points the reader at it, conversationally, as an equal [Practitioner]. His "curse of knowledge", the difficulty of imagining not knowing what you know, is the usual reason experts write badly [Practitioner]. The measured counterpart is stance: students used more modal and epistemic constructions than ChatGPT (Herbold et al. [Study]), and human text carries more negative emotion (Munoz-Ortiz et al. [Study]). A human writer commits to a view and lets feeling show; the model is neutral and positive (inference from those two findings).

**It tells a story with a turn in it.** Narratives raise non-experts' comprehension, interest and engagement (Dahlstrom's review [Study]); more narrative abstracts are cited more (Hillier et al., a correlation [Study]); Olson's "And, But, Therefore" makes the turn the centre of the story [Practitioner]. Stating what people expected and then showing what actually happened teaches better than a clean explanation (Muller and Sharma, effect sizes 0.79 and 0.83 [Study]). Human stories keep ambiguity and loose ends where AI stories tidy them up (StoryScope [Study]).

**It sounds like a person talking.** "Write like you talk"; fragments and colloquialisms belong in scripts (NPR training [Practitioner]). Contractions and first person do read as human (Jakesch et al.), but that heuristic is wrong, so they are worth using because speech has them, not to fool anyone (inference).

## 1.4 Getting human-sounding prose out of an LLM, and what the evidence says about each method

**Instructions that say what to do, with the reason.** Anthropic's documentation says to tell the model what to do rather than what not to do ("Your response should be composed of smoothly flowing prose paragraphs" rather than "Do not use markdown"), to give the reason for a rule because the model "is smart enough to generalize from the explanation", and to write the prompt itself in the style wanted, since "removing markdown from your prompt can reduce the volume of markdown in the output". Its example of a rule with a reason is apt for us: "Your response will be read aloud by a text-to-speech engine, so never use ellipses since the text-to-speech engine will not know how to pronounce them." [Vendor] The evidence is the vendor's own testing, not published. The "pink elephant" study supports the caution about prohibitions: a model told to avoid a named subject and discuss another instead still drifted back to it, and prompting alone did worse than fine-tuning (Castricato et al. [Study, preprint]).

**Naming the anti-pattern.** Anthropic's guidance for one of its newest models defines "mannered prose" (metaphor and flourish in place of direct statement, "a dial worth turning" for "a parameter worth varying") and says an instruction that defines it, or simply "Please remove all mannered prose", helps [Vendor]. The one published test of a targeted instruction I found is small but on point: a one-line instruction against the corrective figure ("not X but Y") cut its use by 70 to 72% in oratory and argument on Claude models (Boggia [Study, preprint, weak]). Targeted instructions against one named construction seem to work; I found no test of long lists of them (inference).

**Banned-phrase lists.** The evidence is mixed to negative as a main method. Russell et al. built a "humaniser" prompt from their experts' own clues and gave it to o1-Pro; the experts still caught every humanised article by majority vote, though they were less confident, and they noticed new tells the instruction introduced, such as honorifics on every name [Study]. OpenAI could not make ChatGPT obey a no-em-dash instruction reliably until a model update in November 2025, and users still reported slips afterwards [News]. Freeburg reports that an explicit em-dash ban fails in some models [Measurement, preprint]. At the decoding level, banning tokens outright wrecks quality when the list is long (a collapse to 28 of 100 on a writing benchmark in Antislop), whereas a backtracking sampler or targeted fine-tuning removes 90% of listed patterns without harm; neither is available through a hosted model's API [Study, preprint]. The inference: a list of banned words is useful as a check run on the draft afterwards, where a hit sends the sentence back for rewriting, and weak as a prompt, where it tends to produce near-synonyms and new tells. The substitution effect is widely reported by practitioners but I found no study that measured it.

**Examples.** Anthropic calls examples "one of the most reliable ways to steer Claude's output format, tone, and structure" and recommends three to five, relevant, varied enough "that Claude doesn't pick up unintended patterns", wrapped in tags [Vendor]. The one study of style imitation found that a few examples bring a model closer to an author's style than none, that gains flatten after four or five, and that informal styles (blogs, forums) are much harder to match than structured ones (news, email) [Study] (Wang et al. 2025). Examples help, but they do not by themselves make a model write like a particular person (inference from that study).

**Editing passes.** The best direct evidence is LAMP: experts preferred writer-edited paragraphs to LLM-edited ones, and LLM-edited ones to the unedited drafts. An automatic pass that first finds problem spans by the seven-category taxonomy (cliche, unnecessary exposition, purple prose, poor sentence structure, lack of specificity, awkward word choice, tense inconsistency) and then rewrites them improved the text, but models found the problem spans with a precision of only 0.46 against the experts' agreement of 0.57 [Study]. So an LLM edit pass helps and a human edit helps more. One caution: LLMs asked to fix grammar only still changed meaning (Abdulhai et al. [Study, preprint]); any edit pass needs a check that facts and numbers survived (inference).

**Training on the target style, and model judges.** In a preregistered study of pastiche of 50 award-winning authors, expert readers strongly preferred MFA writers to prompted models, but preferred models fine-tuned on each author's books, and detectors flagged 97% of the prompted texts against 3% of the fine-tuned ones (Chakrabarty, Ginsburg and Dhillon [Study, preprint]). Prompting leaves the signature; training on a body of target prose removes most of it. The same group found general-purpose LLMs judge writing quality only slightly better than chance, while a reward model trained on expert edits reached 74% and its picks were preferred by writers 66% of the time (Chakrabarty, Laban and Wu 2025 [Study, preprint]); LLM judges also failed to agree with experts on creativity (Chakrabarty et al. 2024 [Study]). A general model asked which draft is better is a weak judge (inference).

**Sampling for diversity.** Asking for several candidates with their probabilities ("verbalized sampling") raised diversity in creative writing by 1.6 to 2.1 times without hurting accuracy (Zhang et al. [Study, preprint]); base models are more varied than aligned ones (West and Potts [Study]). For us that suggests drafting several versions of an important line and choosing, rather than accepting the first (inference).

**Reading aloud.** Every broadcast guide recommends reading the script aloud before recording [Practitioner]. I found no study of it, but it is the direct test of the ear-specific rules in 1.5 (inference).

## 1.5 Which tells matter most heard aloud, as against read on a page

No study I found tested whether listeners detect LLM-written narration scripts. What follows combines the measured tells above with research on listening and with broadcast practice, and the ranking is inference.

**The ear cannot hear typography.** Em dashes, bold, headings, bullets, title case, curly quotes and emoji vanish in narration [Field guide; inference]. They still matter in two places: in on-screen text, which is read, and in the audio itself, because a speech engine turns dashes, colons and ellipses into pauses or mispronunciations (Anthropic's text-to-speech example [Vendor]).

**The ear hears rhythm first.** Uniform sentence length is the measured tell that listening exposes most, since a listener gets the cadence whether or not they attend to words (inference from Desaire, Munoz-Ortiz and Provost). Broadcast guides want short sentences, one idea each, with about 20 words as a working ceiling [Practitioner]; the human pattern is variety within that, some very short sentences and an occasional longer one that runs forward without nesting (inference).

**Oratory figures are louder aloud.** The corrective figure is overused most in speeches, at 2.2 times the human rate (Boggia [Study, weak]), and narration is the register closest to speech-making. The rule of three and the rhetorical question with its reveal ("The result? ...") are rhythmic devices built for the ear, and they repeat audibly (inference; Kriss [Essay]).

**Editorial tails and appraisal words are audible.** A trailing "-ing" clause that asserts meaning arrives at the end of a sentence, where a listener's attention falls (inference), and GPT-4o uses such clauses at 5.3 times the human rate (Reinhart [Study]). Words of significance and praise read as hype when spoken in a narrator's voice (inference). Some of the lexical tells (*delve*, *showcase*, *meticulous*) are now spreading into human podcasts (Yakura [Study]), so a listener may hear them as podcast-speak rather than as AI (inference).

**Abstraction costs more when heard.** A listener cannot re-read, so nominalisations, long words and nested clauses cost comprehension. Fang's Easy Listening Formula (1966) scores broadcast copy by its words of more than one syllable [Study]; center-embedded clauses tax working memory (psycholinguistics, summarised in the Wikipedia article on center embedding); NPR and BBC-trained writers put attribution first, round numbers, give one number per sentence and prefer the active voice [Practitioner]. The LLM default of nominalisations and phrasal coordination at twice the human rate therefore hurts more aloud than on a page (inference).

**The narration and the picture.** For a video, the words and the images are one message. Television news viewers recalled and understood the narration better when it matched the pictures (Drew and Grimes 1987 [Study]). Identical on-screen text and narration generally impairs learning, while short on-screen key terms help (the redundancy principle and the Adesope and Nesbit meta-analysis of 57 studies, [Study]). For our videos this means narration about what is on screen at that moment, and on-screen text that labels rather than transcribes (inference). Captions for viewers watching without sound are a separate, accessibility job.

**The voice.** If the narration is synthesised, the voice is its own tell. Listeners caught deepfake speech 73% of the time (Mai et al. [Study]). NotebookLM's AI podcasts have short turns, almost no overlap and no gaps, because the whole dialogue is scripted before it is voiced (Carruthers and Heim, as reported [Study]). A single-voice script read at an even pace with even sentences will sound more synthetic than one whose sentences vary (inference).

---

# Part 2. Source catalogue

Each entry says what the source is and who made it, what it studied and how, what it found, what it says in its own words, where it is weak, and what it means for our prose. Entries are grouped by kind.

## 2.A Guides and essays about the tells

### Wikipedia, "Signs of AI writing" [Field guide]

URL: https://en.wikipedia.org/wiki/Wikipedia:Signs_of_AI_writing (raw text: https://en.wikipedia.org/w/index.php?title=Wikipedia:Signs_of_AI_writing&action=raw). Maintained by WikiProject AI Cleanup, which formed in 2023 to find and remove undisclosed AI-generated content from Wikipedia (https://en.wikipedia.org/wiki/Wikipedia:WikiProject_AI_Cleanup). It is an advice page, not policy, built from thousands of real cases in articles and drafts, each category illustrated with examples taken from Wikipedia. It is a living document; I read it on 2026-09-26.

What it lists, under content: undue emphasis on significance, legacy and broader trends ("stands/serves as, is a testament/reminder, crucial/pivotal/vital role, underscores importance, reflects broader, symbolizing its ongoing, contributing to the, setting the stage for, represents a shift, key turning point"), with the explanation that LLMs regress to the statistical mean and replace specific facts with generic positive description; canned emphasis on notability and media coverage; superficial analyses, meaning "-ing phrases at sentence ends" such as "highlighting/underscoring/emphasizing, reflecting/symbolizing, contributing to, cultivating/fostering, enhancing, valuable insights, align/resonate with" ("AI chatbots tend to insert superficial analysis of information, often in relation to its significance, recognition, or impact"); promotional language ("boasts a, vibrant, rich, profound, enhancing, showcasing, exemplifies, commitment to, natural beauty, nestled, in the heart of, groundbreaking, renowned, featuring, diverse array"), noting that GPT-4 was blatantly positive and newer models are subtly so; vague attributions ("Industry reports, Observers have cited, Experts argue, Some critics argue, several sources"); and outline-like conclusions about challenges and future prospects ("Despite its ... faces several challenges ...", "Despite these challenges", "Future Outlook").

Under language and grammar: dated AI vocabulary lists (GPT-4, 2023 to mid-2024: "Additionally, boasts, bolstered, crucial, delve, emphasizing, enduring, garner, intricate/intricacies, interplay, key, landscape, meticulous/meticulously, pivotal, underscore, tapestry, testament, valuable, vibrant"; GPT-4o, mid-2024 to mid-2025: "align with, bolstered, crucial, emphasizing, enhance, enduring, fostering, highlighting, pivotal, showcasing, underscore, vibrant"; GPT-5, mid-2025 on: "emphasizing, enhance, highlighting, showcasing"; Grok: "causal, empirical, correlate, underscore"); avoidance of basic copulatives ("serves as/stands as/marks/functions as/operates as/represents [a], boasts/features/maintains/offers [a]"), citing a documented 10% fall in "is/are" in 2023 academic writing; vague expressions of association ("in connection with", "associated with"); negative parallelisms ("not only ... but", "It's not ..., it's ...", "no ..., no ..., just ..."), with the note that such output "may seem as though it is clearing up a common misconception"; and the rule of three: "LLMs overuse the rule of three. This can take different forms, from 'adjective, adjective, adjective' to 'short phrase, short phrase, and short phrase'."

Under style: title case in headings, overuse of boldface, inline-header vertical lists, overuse of em dashes, emoji as formatting, unusual tables, curly quotation marks. Under communication: collaborative phrases addressed to the user, knowledge-cutoff disclaimers, placeholder text. It also lists citation and markup artifacts particular to Wikipedia, and, as historical indicators of older models, didactic disclaimers, section summaries and "elegant variation" (swapping synonyms for the same thing).

Its caveats are as useful as its lists. One or two AI words are coincidence and many together a strong sign; word preferences change over time; humans write promotionally, use weasel words and myth-busting "not X but Y" too; "These are potential signs of a problem, not the problem itself"; humans are increasingly influenced by LLMs; and "Humans are notoriously bad at distinguishing human and LLM-generated text ... a 2025 study has shown that human ability to distinguish LLM text from human is no better than random chance", while heavy LLM users reach about 90%.

Limits: it is built for encyclopedic prose, where puffery and editorialising are policy violations anyway, and from cases editors happened to catch, so it over-represents the obvious ones. It gives no frequencies.

For our prose: the content and grammar categories carry straight over to narration (significance claims, "-ing" tails, copula avoidance, "not X but Y", threes, vague attribution, tidy conclusions). The formatting categories matter for on-screen text only.

### Sam Kriss, "Why Does A.I. Write Like ... That?" [Essay]

New York Times Magazine, 3 December 2025. I could not load the Times page; I read it through excerpts at https://longreads.com/2025/12/04/why-does-a-i-write-like-that/ and https://rogerwong.me/2026/01/why-does-ai-write-like-that . Kriss is a British essayist. The essay catalogues AI prose habits and argues that they come from the model trying to write well by following rules about good writing too literally.

The tells it names: the "It's not X, it's Y" construction, the rule of threes, the overuse of words like *delve*, em dashes and negation. Its explanation, in the quoted passages: the model "knows that good writing involves subtlety", and so it "screams at the top of its voice about how absolutely everything in sight is shadowy, subtle and quiet"; "Everything that isn't a ghost is usually woven"; it keeps "inviting you, like an explorer standing on the threshold of some half-buried temple, to *delve in*"; the tone is "overeager, insipid", "on the verge of some kind of hysteria", with interjections like "And honestly? That's amazing." It also reports absurd chapter titles produced by AI ("The Wetness of the Potatoes").

Limits: an essay, with no counts; my reading is second-hand.

For our prose: the useful idea is the mechanism. A model told to be vivid, subtle or engaging performs vividness, subtlety and engagement, so style instructions phrased as qualities ("make it gripping") invite the tells (inference). Instructions should name concrete behaviours instead.

### Ted Chiang, "Why A.I. Isn't Going to Make Art" [Essay]

The New Yorker, "The Weekend Essay", August 2024: https://www.newyorker.com/culture/the-weekend-essay/why-ai-isnt-going-to-make-art (read through quotations in search results and reviews). Chiang is a science-fiction writer.

His argument: "art requires making choices at every scale; the countless small-scale choices made during implementation are just as important to the final product as the few large-scale choices made during the conception." A model filling in those choices takes "an average of the choices that other writers have made, as represented by text found on the Internet; that average is equivalent to the least interesting choices possible, which is why A.I.-generated text is often really bland." Critics reply that humans are not very original either and that "choices" is too narrow a definition of art.

Limits: an argument, not a measurement, though the measured narrowing in Reinhart, West and Potts, Zhang et al. and Doshi and Hauser is consistent with it.

For our prose: each sentence should contain at least one choice the model would not make by default: the particular fact, the exact word for what happened, the order that creates surprise (inference).

## 2.B Measurements of LLM style

### Kobak, Gonzalez-Marquez, Horvat and Lause, "Delving into LLM-assisted writing in biomedical publications through excess vocabulary" [Study, Measurement]

Science Advances, 2 July 2025 (preprint June 2024): https://www.science.org/doi/10.1126/sciadv.adt3813 ; open text https://pmc.ncbi.nlm.nih.gov/articles/PMC12219543/ ; preprint https://arxiv.org/abs/2406.07016 ; code https://github.com/berenslab/llm-excess-vocab . The authors are computational scientists at the University of Tubingen and Northwestern.

Method: they counted, for every word, its yearly frequency in over 15 million PubMed abstracts from 2010 to 2024, projected each word's 2024 frequency from its 2021 to 2022 trend, and called a word "excess" when its real 2024 frequency exceeded the projection. The same method applied to earlier years picks up the vocabulary of events (COVID, for example), which gives a baseline for what a normal vocabulary shift looks like.

Findings: at least 13.5% of 2024 abstracts were processed with LLMs, a lower bound that reached 40% in some subcorpora (by discipline, country and journal). The excess words of 2024 were style words, not content words: 66% of the 379 excess style words were verbs and 14% adjectives, whereas earlier excess vocabulary was mostly nouns ("almost entirely of content words" in the COVID years). Among rare words, the frequency ratios were *delves* 28.0, *underscores* 13.8, *showcasing* 10.7. Among common words the largest absolute gaps were *potential*, *findings* and *crucial*.

Limits: one genre (abstracts), and "processed with LLMs" includes light editing. The ratios are for scientific English and will not transfer unchanged to narration.

For our prose: the tells are in the verbs and adjectives that appraise, emphasise and connect. A sentence built on a plain verb of action (the creature ate, grew, died, sank) avoids most of them (inference).

### Reinhart, Markey, Laudenbach, Pantusen, Yurko, Weinberg and Brown, "Do LLMs write like humans? Variation in grammatical and rhetorical styles" [Study]

PNAS 122(8), e2422455122, February 2025: https://www.pnas.org/doi/10.1073/pnas.2422455122 ; preprint https://arxiv.org/abs/2410.16107 . The authors are statisticians and linguists at Carnegie Mellon.

Method: two parallel corpora. HAP-E has 12,000 texts across six genres (academic, news, fiction, spoken word, blogs, TV and movie scripts), 8,290 usable after cleaning; CAP is drawn from the Corpus of Contemporary American English across eight registers, 9,615 texts. From each human text they took two consecutive chunks of about 500 words, gave the first to a model with the instruction to "write 500 more words in the same style, tone, and diction", and compared the model's continuation with the real second chunk. Models: GPT-4o, GPT-4o mini, and Llama 3 8B and 70B, each in base and instruction-tuned form. Features: Douglas Biber's 66 lexical, grammatical and rhetorical categories.

Findings: instruction-tuned models differ systematically from humans, and the differences are larger than those of base models, which "use features at rates similar to human texts". GPT-4o used present participial clauses at 5.3 times the human rate (Cohen's d = 1.38), nominalisations at 2.1 times (d = 1.23), "that" clauses as subjects at 2.6 times, phrasal coordination at 1.9 times, and agentless passives at about half the human rate. Instruction-tuned Llama used participial clauses at 2 to 5 times and nominalisations at 1.5 to 2 times. GPT-4o used downtoners more than humans; all Llama variants avoided them. Words GPT-4o used over a hundred times more often than humans: *camaraderie* (162 times), *tapestry* (155), *intricate* (119), *underscore* (107), *amidst* (100); *tapestry* appeared in 23% of GPT-4o texts and *amidst* in 27%. A random forest on the features told the seven sources apart at 66% (chance 14%) and a human text from each specific model at 93 to 98%, and only 4.2% of LLM texts were classified as human. The differences persisted from smaller to larger models. The authors: "Instruction tuning pushes models to produce text that reads *unlike* a human."

Limits: the task was continuation of a human text, which is not how we use a model; the models are 2024 ones.

For our prose: the participial tail and the nominalisation are the two strongest grammatical tells with numbers behind them, and both are measured in the very style-matching setting where a model is trying to sound like a given human. Asking a model to "match this style" does not remove them.

### Juzek and Ward, "Why Does ChatGPT 'Delve' So Much? Exploring the Sources of Lexical Overrepresentation in Large Language Models" [Study]

COLING 2025; preprint 16 December 2024: https://arxiv.org/abs/2412.11385 . Florida State University (a linguist and a philosopher of science).

Method: they identified 21 focal words whose rise in scientific abstracts is likely due to LLMs (*delve*, *intricate*, *underscore* among them), then looked for causes: model architecture, algorithm choices, training data, and preference training (RLHF), the last by comparing models and by an exploratory online study of which texts people prefer.

Findings: no evidence for architecture, algorithm or training data as the cause; the model comparisons are consistent with RLHF playing a role; in the online study participants seemed to react differently to *delve* than to the other focal words.

Limits: exploratory; the causal question stays open.

For our prose: the lexical tells are probably learned from what human raters rewarded, so they will keep changing with each model, and a fixed banned list will date (inference).

### Yakura, Lopez-Lopez, Brinkmann, de la Serna, Kirfel, Gupta, Soraperra, Eisenmann, Wulff and Rahwan, "Empirical evidence of Large Language Model's influence on human spoken communication" [Study, preprint]

arXiv, first version 3 September 2024, fourth version 16 July 2026: https://arxiv.org/abs/2409.01754 ; data and code https://zenodo.org/records/21298066 . Max Planck Institute for Human Development and collaborators.

Method: a per-word "GPT score" from comparing human texts with LLM rewrites of them; a synthetic-control analysis of 737,083 hours of conversation from 824,634 podcast episodes screened for unscripted speech, plus YouTube transcripts; a Bayesian change-point analysis; a preregistered experiment with 496 people who had a short text chat with a chatbot; and an agent-based model.

Findings: words ChatGPT prefers (*delve, showcase, boast, intricacies, meticulous*) rose abruptly in spontaneous human speech after ChatGPT's release; the reported rise in the use of AI-introduced words was 36 percentage points, comparable to lexical alignment between human speakers in dialogue. The rise was significant in science and technology, business and education, and absent in sports and religion. A brief chatbot exchange made people use those words in their own speech afterwards, an effect that survived distraction.

Limits: a preprint under revision; the effect-size figure comes from the project record's summary, not the abstract.

For our prose: the AI vocabulary is now also podcast vocabulary in exactly the domain of our videos (science and technology). A listener may hear it as AI or as generic science-talk; either way it is not ours (inference).

### Desaire, Chua, Isom, Jarosova and Hua, "Distinguishing academic science writing from humans or ChatGPT with over 99% accuracy using off-the-shelf machine learning tools" [Study, Measurement]

Cell Reports Physical Science, 7 June 2023: https://pmc.ncbi.nlm.nih.gov/articles/PMC10328544/ (also https://www.sciencedirect.com/science/article/pii/S266638642300200X). University of Kansas chemists.

Method: 64 "Perspectives" articles from Science (September 2022 to March 2023) and 128 ChatGPT (GPT-3.5) texts written to prompts asking for 300 to 400 word pieces on the same topics; 1,276 training paragraphs; test sets of 180 documents (about 1,200 paragraphs) from 2020 to 2021. An XGBoost classifier on 20 hand-chosen features.

Findings: 100% accuracy at document level and 92% at paragraph level on the test sets. The features and which side had more of each: sentences per paragraph (human), words per paragraph (human), parentheses (human), dashes (human), semicolons or colons (human), question marks (human), single quotes (ChatGPT), standard deviation of sentence length (human), length difference between consecutive sentences (human), sentences under 11 words (human), sentences over 34 words (human), "although", "However", "but", "because" and "this" (human), "others" or "researchers" (ChatGPT), numbers (human), capitals (human), "et" as in et al. (human). In the authors' words: "Humans vary their sentence lengths more than ChatGPT. Humans also more frequently used longer sentences (35 words or more) and shorter sentences (10 words or fewer)." Scientists used equivocal words (*but, however, although*) more.

Limits: one genre, one old model, one kind of prompt; the authors say so. The dash finding has since reversed.

For our prose: sentence-length variance and consecutive-sentence contrast are the measured rhythm signal. The words *but* and *because* are human markers: a narration that reasons (this happened because; they expected this but) reads less like a model than one that lists (inference).

### Munoz-Ortiz, Gomez-Rodriguez and Vilares, "Contrasting Linguistic Patterns in Human and LLM-Generated News Text" [Study]

Artificial Intelligence Review 57, 265 (2024); preprint August 2023: https://arxiv.org/abs/2308.09067 . Universidade da Coruna.

Method: English news written by humans compared with news generated by six LLMs from three families and four sizes, across morphological, syntactic, psychometric and sociolinguistic measures. (The summary I read did not name the models or the size of the news corpus.)

Findings: human texts have "more scattered sentence length distributions", more varied vocabulary, a distinct use of dependency and constituent types, shorter constituents and more optimised dependency distances; humans express stronger negative emotions (fear, disgust) and less joy; LLM outputs use "more numbers, symbols and auxiliaries (suggesting objective language) than human texts, as well as more pronouns"; LLM toxicity rises with model size; and "Differences between LLMs and humans are larger than between LLMs."

Limits: older models (2023); the number finding conflicts with Desaire's, which suggests register matters.

For our prose: human reports let fear and disappointment in. A narration about an experiment that failed can say so plainly, without a consoling turn (inference).

### Herbold, Hautli-Janisz, Heuer, Kikteva and Trautsch, "A large-scale comparison of human-written versus ChatGPT-generated essays" [Study]

Scientific Reports, 2023: https://www.nature.com/articles/s41598-023-45644-9 ; preprint https://arxiv.org/abs/2304.14276 . University of Passau.

Method: argumentative essays by German high-school students on an essay forum compared with essays by ChatGPT-3 and ChatGPT-4 on the same topics, rated by teachers against a rubric and compared on computed linguistic features. (Sample sizes were not in the summary I read.)

Findings: the AI essays were rated higher on every rubric criterion, GPT-4 above GPT-3; their style differed: more nominalisations and more complex sentences ("more complex, 'scientific', language"), fewer discourse and epistemic markers, greater lexical diversity; students made more use of modal and epistemic constructions, "which tend to convey speaker attitude".

Limits: the human writers were school students, not skilled writers.

For our prose: lexical diversity is not a sign of human writing; models vary their words more than students did. What the students had and the models lacked was stance: *might, probably, I think, seems*. A narrator who says what surprised them, or what nobody knows yet, supplies it (inference).

### Shaib, Elazar, Li and Wallace, "Detection and Measurement of Syntactic Templates in Generated Text" [Study]

EMNLP 2024: https://arxiv.org/abs/2407.00211 ; https://aclanthology.org/2024.emnlp-main.368/ . Northeastern University, Allen Institute for AI, University of Texas at Austin.

Method: they tag text with parts of speech, find repeated part-of-speech sequences ("templates") and measure how often texts contain them (template rate, templates per token, compression ratio of the part-of-speech sequence), in model output and in human text, and check whether the templates appear in the models' pre-training data.

Findings: LLMs produce templated text at high rates; 76% of the templates in model-generated text can be found in pre-training data, against 35% for human text; the templates are not overwritten by fine-tuning or alignment.

Limits: templates are measured on part-of-speech sequences, which catch structure but not meaning.

For our prose: the sameness of AI prose sits below the word level, in repeated sentence shapes. Replacing words keeps the shape, so an edit pass has to vary the construction of sentences, not only their vocabulary (inference).

### Shaib, Chakrabarty and others, "Measuring AI 'Slop' in Text" [Study, preprint]

arXiv, September 2025: https://arxiv.org/abs/2509.19163 ; a university write-up at https://www.khoury.northeastern.edu/ai-slop-is-a-common-online-nuisance-but-what-makes-a-piece-of-text-slop/ .

Method: interviews with experts in NLP, writing and philosophy produced a taxonomy of what "slop" means; the authors mapped it to measurable dimensions and annotated texts at the span level.

Findings: three themes. Information utility: density (substantive content per word, operationalised as token entropy under GPT-2, mean and coefficient of variation) and relevance to the task. Information quality: factuality (errors, hallucination, fallacy). Style quality: repetition (the same words and phrases, low vocabulary diversity) among others. Whole-text "slop or not" judgments were somewhat subjective, but they correlated with coherence and relevance.

Limits: the operational measures (GPT-2 entropy as density) are proxies.

For our prose: slop is not only style. A sentence that carries no new fact is slop even when every word is fine, so the first test of each sentence is what it tells the viewer that the previous one did not (inference).

### Paech and colleagues, "Antislop: A Comprehensive Framework for Identifying and Eliminating Repetitive Patterns in Language Models" [Study, preprint]

arXiv, 16 October 2025, revised 21 October 2025 (listed under ICLR 2026 in one index): https://arxiv.org/abs/2510.15061 ; Thoughtworks summary https://research.thoughtworks.com/library/antislop-framework-identifying-eliminating-repetitive-patterns-language-models . Code under MIT licence.

Method: a pipeline that profiles each model's over-represented words, phrases and trigrams against human baselines; the Antislop sampler, which backtracks during generation when a banned string appears and resamples; and Final Token Preference Optimization (FTPO), a fine-tuning method that adjusts the logits of single tokens wherever a banned pattern appeared in a generation trace. Compared with plain token banning and with DPO.

Findings: some patterns appear over 1,000 times more often in model output than in human text. Examples: the name *Elara* at 85,513 times its human frequency, "heart hammered ribs" at 1,192 times, "voice trembling slightly" 731 times, "said voice devoid" 693 times, plus "voice barely above a whisper", *Kael* and "profound sense"; "It's not X, it's Y" at 6.3 times the human rate in some models. The sampler suppressed more than 8,000 patterns while keeping quality, whereas token banning "becomes unusable at just 2,000" and collapsed to a writing score of 28 out of 100. FTPO removed 90% of slop while keeping or improving scores on GSM8K, MMLU and creative writing; DPO lost 6 to 15 points of writing quality for 80 to 82% suppression. The paper names the "pink elephant problem" (suppression by instruction can backfire) but, in the summary I read, does not test prompting.

Limits: the methods need access to the model's decoding or weights; the patterns profiled are mostly fiction.

For our prose: the two methods that work are not available through a hosted API; what we can take from it is the list-and-check pipeline: profile the model's favourite phrases against human narration, then scan every draft for them (inference).

### Boggia, "Artificial Epanorthosis: Why large language models overuse a classical rhetorical figure, and how to mitigate it" [Study, preprint, weak]

arXiv, 23 July 2026, revised 28 July 2026: https://arxiv.org/abs/2607.21498 (full text https://arxiv.org/html/2607.21498). Single author.

Method: a two-stage detector (lexical markers such as "but", "rather", "I mean" and "not ... but" frames, then a construction-level classifier) run on model output and on pre-2022 human corpora by register: Wikipedia (encyclopedic), Wikinews and pre-2022 Common Crawl news (journalism), arXiv abstracts (academic), Conan Doyle (fiction), Stack Exchange answers (informal Q&A), Mill's On Liberty (argument), and addresses by Lincoln, Jefferson and T. Roosevelt (oratory). Models: Claude Haiku, Sonnet and Opus. Mitigation tested three ways: a one-line instruction, LoRA adapters and supervised fine-tuning on an open Qwen2.5-7B model.

Findings: the figure ("This is not a course. It is a journey of transformation") is mis-calibrated by register in both directions. Oratory: 33.5 against 14.9 per 10,000 words, 2.2 times the human rate (p = 0.03). Academic abstracts: 7.8 against 3.6 (not significant). Informal Q&A: 1.3 against 8.2, a fifth of the human rate. Argument, journalism and encyclopedic writing: parity. The author attributes the overuse to promotional prose in training data and to preference tuning that rewards confident phrasing, with left-to-right generation as an amplifier. A one-line instruction (in the Italian demonstration, against "Non e X. E Y", "non X ma Y" and "anzi", asking for affirmative statements) cut use by 72% in oratory (p = 0.03), 70% in argument (p = 0.004) and 48% in promotional text (not significant); no substitution by other patterns or loss of quality was reported, though content fidelity was checked only informally. Fine-tuned adapters removed the figure almost entirely, with a scale to tune it back to human rates.

Limits, as the author lists them: small samples per cell (17 to 28 windows), uncorrected p-values, historical baselines against modern models, detector agreement with human annotators of kappa 0.35 (precision 0.17 on human text against 0.82 on model text), one model family, English measurement with an Italian mitigation, pilot-scale mitigation without significance tests, and a causal attribution to RLHF that is inference.

For our prose: weak evidence, but on the right models and the right register. It suggests the figure is overused specifically in speech-like writing, and that one plain sentence in the prompt, telling the model to state things affirmatively, cuts it by about two thirds. The goal the author proposes, human rates rather than zero, fits ours: keep the figure for a real expectation overturned (inference).

### Freeburg, "The Last Fingerprint: How Markdown Training Shapes LLM Prose" [Measurement, preprint]

arXiv, 27 March 2026: https://arxiv.org/abs/2603.27006 (html https://arxiv.org/html/2603.27006v1). An independent researcher. Seen through the abstract page and search-result snippets.

Method: twelve models from five providers (Anthropic, OpenAI, Meta, Google, DeepSeek) on matched prompts, em dashes counted per 1,000 words against a human control; a two-condition suppression experiment, a three-condition gradient and a base-against-instruct comparison.

Findings: in standard essays GPT-4.1 produced 10.62 em dashes per 1,000 words, 3.3 times the human baseline of 3.23; Claude Opus 4.6 9.09; DeepSeek V3 6.95; Meta's Llama models none. GPT-4.1 under a suppression instruction still produced 9.1 per 1,000 words, and an explicit prohibition failed to remove the dash in some models. The tendency exists in base models before RLHF. The author's explanation: the em dash is "markdown leaking into prose", structure learned from markdown-saturated training data and amplified by post-training.

Limits: a single-author preprint I did not read in full; sample sizes were not in what I saw.

For our prose: in narration the dash is silent but it marks a sentence built as statement plus aside, which a speech engine renders as a pause or nothing. On screen it is visible. Rewrite dash asides as their own sentences rather than swapping the dash for a comma (inference).

### Czuma, "Em-ergence of the em-dash: a population-level rise in em-dash frequency in medRxiv preprints at the dawn of the large-language-model era" [Measurement, preprint]

arXiv, 28 June 2026: https://arxiv.org/abs/2606.29540 . Single author.

Method: all first-version medRxiv full-text preprints from 2020 to 2025 whose Discussion section had at least 500 characters (69,632 preprints), coded for whether the Discussion used an em dash, before and after 30 November 2022.

Findings: 4.23% before ChatGPT and 11.58% after, an absolute rise of 7.35 points (95% CI 6.94 to 7.77), odds ratio 2.96 (2.77 to 3.17). The rise was gradual: about 4% through 2023, 8.0% in 2024 and 20.3% in 2025.

Limits: presence or absence only; association with LLM use is inferred from timing.

For our prose: the em dash has become an AI marker in readers' minds whether or not a given dash came from a model (inference).

### Sam Altman on ChatGPT and em dashes [News]

Post on X, 14 November 2025: https://x.com/sama/status/1989193813043069219 ("Small-but-happy win: If you tell ChatGPT not to use em-dashes in your custom instructions, it finally does what it's supposed to do!"); report and users' tests at https://www.pcworld.com/article/2977726/openai-has-fixed-chatgpts-infamous-em-dash-obsession.html and https://slashdot.org/story/25/11/14/2129248/sam-altman-celebrates-chatgpt-finally-following-em-dash-formatting-rules .

What it shows: for months ChatGPT ignored instructions not to use em dashes; after an update it followed them, though it still used them by default and some users showed slips.

For our prose: even a simple, explicit, single-character prohibition was not reliably followed by a leading model until its maker trained for it. A banned list needs a mechanical check afterwards (inference).

## 2.C Narrative, creativity and editing

### Chakrabarty, Laban, Agarwal, Muresan and Wu, "Art or Artifice? Large Language Models and the False Promise of Creativity" [Study]

CHI 2024: https://arxiv.org/abs/2309.14556 ; https://dl.acm.org/doi/fullHtml/10.1145/3613904.3642731 .

Method: the authors built the Torrance Test of Creative Writing (TTCW), 14 yes-or-no tests grouped under the four dimensions of the Torrance Test of Creative Thinking (fluency, flexibility, originality, elaboration), from a formative study with creative-writing experts, using the Consensual Assessment Technique. Ten creative writers applied it to 48 stories, some by professional authors and some by LLMs of 2023 (the summary I read named neither the source of the professional stories nor the models). They also tried LLMs as judges.

Findings, in the abstract's words: "LLM-generated stories pass 3-10X less TTCW tests than stories written by professionals", and "none of the LLMs positively correlate with the expert assessments" as judges.

Limits: small (48 stories), fiction only, 2023 models.

For our prose: an LLM is a poor judge of its own creative quality, so an LLM check of "is this good writing" is weak evidence; checks that count concrete things are stronger (inference).

### Russell, Rajendhran, Pham, Iyyer and Wieting, "StoryScope: Investigating idiosyncrasies in AI fiction" [Study, preprint]

arXiv, 3 April 2026, revised 10 August 2026: https://arxiv.org/abs/2604.03136 .

Method: a parallel corpus of 10,272 writing prompts, each answered by a human author and by five LLMs, giving 61,608 stories of about 5,000 words; each story described by discourse-level narrative features across 10 dimensions (not listed in what I read); classifiers trained on narrative features alone.

Findings: narrative features alone separate human from AI stories at 93.2% macro-F1 and attribute a story to one of six sources at 68.4%; they capture "over 97% of the performance of models that include stylistic cues"; a compact set of 30 features carries most of the signal. "AI stories over-explain themes and favor tidy, single-track plots while human stories frame protagonist' choices as more morally ambiguous and have increased temporal complexity." AI stories cluster in a shared region of narrative space; human stories are more diverse. Model signatures: "Claude produces notably flat event escalation, GPT over-indexes on dream sequences, and Gemini defaults to external character description."

Limits: fiction of 5,000 words; our pieces are short non-fiction.

For our prose: the tells survive at the level of story shape even when the wording is clean. For an experiment's story that means: do not state the theme (the story shows it), keep the second thread if there was one, let events escalate unevenly as they did, and keep what stayed unresolved (inference).

### Doshi and Hauser, "Generative AI enhances individual creativity but reduces the collective diversity of novel content" [Study]

Science Advances 10(28), July 2024: https://www.science.org/doi/10.1126/sciadv.adn5290 ; summary https://www.sciencedaily.com/releases/2024/07/240712222127.htm ; preprint https://papers.ssrn.com/sol3/papers.cfm?abstract_id=4535536 . UCL School of Management and University of Exeter.

Method: an online experiment with about 300 writers asked to write eight-sentence micro stories for young adults, in three conditions: no AI, one three-sentence story idea from ChatGPT, or up to five such ideas; about 600 evaluators rated the stories.

Findings: with the most AI access, stories scored 8.1% higher on novelty and 9% higher on usefulness; for the less creative writers, gains were larger (10.7% novelty, 11.5% usefulness, 26.6% better written, 22.6% more enjoyable, 15.2% less boring). But stories built on AI ideas were more similar to each other: a 10.7% increase in similarity with one AI idea against no AI. The authors frame it as a social dilemma: each writer does better, and collectively the pool is narrower.

Limits: tiny stories, one model, ideas rather than text.

For our prose: a series of videos whose stories all come from the same model will drift toward one shape, even if each is good. Vary the structure across videos on purpose (inference).

### Chakrabarty, Laban and Wu, "Can AI writing be salvaged? Mitigating Idiosyncrasies and Improving Human-AI Alignment in the Writing Process through Edits" (the LAMP corpus) [Study]

CHI 2025: https://arxiv.org/abs/2409.14509 (html https://arxiv.org/html/2409.14509v4) ; https://dl.acm.org/doi/full/10.1145/3706598.3713559 .

Method: 18 professional writers with MFAs in creative writing (8 in a formative study, 10 more for annotation). About 1,200 paragraphs were taken from published writing: literary fiction from The New Yorker (80% of selections), travel writing and food writing from the New York Times, Modern Love personal essays and the Dear Sugar advice column. GPT-4o wrote an open-ended prompt for each paragraph ("instruction backtranslation"), giving 1,057 prompts, and GPT-4o, Claude 3.5 Sonnet and Llama 3.1 70B wrote paragraphs to them. Writers edited those paragraphs, marking each edit with a category of a seven-part taxonomy they had agreed on. The resulting LAMP corpus ("Language model Authored, Manually Polished") has 1,057 paragraphs and 8,035 edits. A large preference study then compared writer-edited, LLM-edited and unedited paragraphs, and a two-stage automatic pipeline (detect problem spans with few-shot prompts of 2, 5 or 25 examples, then rewrite them) was tested with the same three models.

The taxonomy: cliche ("phrases, ideas, or sentences overused to the point of losing their original impact or meaning"); unnecessary or redundant exposition (restating the obvious, against "show, don't tell"); purple prose ("excessively elaborate writing that disrupts the narrative flow by attracting undue attention to its flamboyant style"); poor sentence structure (run-ons, clumsy transitions); lack of specificity and detail (an "overly general approach fails to engage readers, leaving them unable to visualize scenes"); awkward word choice and phrasing (misused or over-used words, unclear pronouns, over-used passive); tense inconsistency.

Findings: the most frequent edits were awkward word choice and phrasing (28%), poor sentence structure (20%), unnecessary or redundant exposition (18%) and cliche (17%). Cliche edits fell as quality rose; lack-of-specificity edits stayed about constant. Edits were 74% replacements, 18% deletions and 8% insertions; 70% preserved meaning. No model family wrote better than the others. Preference: "Writer-edited > LLM-edited > LLM-generated". Automatic detection of problem spans reached a precision of 0.46 at best (Claude 3.5 Sonnet and GPT-4o with five examples), below the experts' agreement with each other (0.57); assigning the right category was harder still.

Examples from the paper. Cliche: "a tapestry of modernity threaded with the hum of traffic" was deleted. Redundant exposition: "During the quarantine, the days stretched like endless corridors, each more indistinguishable from the last" became "The days blurred into themselves during the quarantine, and I couldn't tell one from the other". Purple prose: "The sobs emerged from this deep well of unspoken expectations, leaving behind a residue of weary resilience and a few hopeful echoes yet unwilling to completely extinguish" became "She cried. She cried deep from this well of scraped knees she bandaged alone".

Limits: creative prose, not science narration; the preference study's size was not in what I read.

For our prose: this is the best evidence for an editing pass and the best checklist for one. The taxonomy maps directly onto narration, and the finding that specificity problems persist after cliches are fixed says where to look last and hardest. The examples also show what the fix looks like: fewer words, plainer images, the concrete thing named.

### Abdulhai, White, Wan, Qureshi, Leibo, Kleiman-Weiner and Jaques, "How LLMs Distort Our Written Language" [Study, preprint]

arXiv, 18 March 2026, revised 26 August 2026: https://arxiv.org/abs/2603.18161 .

Method: three studies of LLM writing assistance (details beyond the abstract's figures were not in what I read).

Findings: a 70% increase in neutral essays among heavy LLM users compared with controls; 21% of peer reviews at a major AI conference identified as LLM-generated, scoring papers a point higher on average and weighting clarity and significance less; and LLMs changed semantic meaning even when told to make grammatical corrections only.

Limits: a preprint; I saw the summary only.

For our prose: a model asked to polish a script will also flatten its stance and may change its facts. Every edit pass is followed by a check that the numbers and claims are unchanged (inference).

### Chakrabarty, Ginsburg and Dhillon, "Readers Prefer Outputs of AI Trained on Copyrighted Books over Expert Human Writers" [Study, preprint]

arXiv, 15 October 2025, revised 17 March 2026: https://arxiv.org/abs/2510.13939 . A computer scientist, a copyright scholar (Columbia Law) and an information scientist.

Method: a preregistered study. MFA-trained writers and three AI systems (ChatGPT, Claude, Gemini) each wrote excerpts of up to 450 words emulating the styles of 50 award-winning authors. The AI worked in two ways: in-context prompting, and a model fine-tuned on each author's complete works. 28 MFA-trained readers and 516 college-educated general readers made blind pairwise comparisons for stylistic fidelity and for quality; leading AI detectors were run on every text.

Findings: with prompting alone, the expert readers strongly preferred the human writers (odds ratio for fidelity 0.16, for quality 0.13), while general readers showed no preference on fidelity and favoured the AI on quality (1.82). Fine-tuning reversed this: the experts favoured the fine-tuned AI (fidelity 8.16, quality 1.87) and the general readers more strongly still (16.65 and 5.42). Detectors flagged 97% of the prompted texts as AI and 3% of the fine-tuned texts.

Limits: literary pastiche of named authors, not a house style; fine-tuning a frontier model on a style is not something a hosted API usually offers.

For our prose: the best available evidence that prompting leaves a model's signature intact for skilled readers and detectors alike, while training on a body of the target style removes most of it. For us, without fine-tuning, the practical consequence is the edit pass and a human editor (inference).

### Chakrabarty, Laban and Wu, "AI-Slop to AI-Polish? Aligning Language Models through Edit-Based Writing Rewards and Test-time Computation" [Study, preprint]

arXiv, 10 April 2025, revised 12 August 2025: https://arxiv.org/abs/2504.07532 .

Method: five writing-preference datasets merged into a Writing Quality Benchmark of about 4,700 judgments; LLMs tested as judges on it; specialised Writing Quality Reward Models trained on edit data such as LAMP; a test-time "generate, edit, rank" procedure that produces several revisions and picks the one the reward model scores highest; nine experienced writers judged the results.

Findings: general-purpose LLMs judged writing quality only slightly better than chance on the benchmark; the specialised reward models reached 74% accuracy and held up on four out-of-distribution sets; the writers preferred the reward model's choice in 66% of cases, and in 72.2% when its score gap exceeded one point.

Limits: the reward models are research artefacts.

For our prose: asking a general model "which version is better written?" is close to a coin toss, so candidate lines should be chosen by a person, by a mechanical check, or by a targeted question ("which of these names a specific thing that happened?") rather than a general quality verdict (inference).

## 2.D How well people detect AI text, and how they react to it

### Clark, August, Serrano, Haduong, Gururangan and Smith, "All That's 'Human' Is Not Gold: Evaluating Human Evaluation of Generated Text" [Study]

ACL 2021: https://aclanthology.org/2021.acl-long.565/ ; preprint https://arxiv.org/abs/2107.00061 . University of Washington and the Allen Institute for AI.

Method: non-expert evaluators judged whether stories, news articles and recipes were written by a human, GPT-2 or GPT-3; three quick training methods were tried (detailed instructions, annotated examples, paired examples). Sample sizes were not in what I read.

Findings: untrained evaluators told GPT-3 text from human text at chance. Training raised accuracy to at most 55% and did not significantly improve it across the three domains. Evaluators' stated reasons were inconsistent and often contradictory across domains.

Limits: GPT-3 era; untrained crowd workers.

For our prose: the general audience's gut sense is unreliable in both directions, so "sounds fine to me" from a casual reader is not a test (inference).

### Jakesch, Hancock and Naaman, "Human heuristics for AI-generated language are flawed" [Study]

PNAS, 7 March 2023: https://www.pnas.org/doi/10.1073/pnas.2208839120 ; preprint https://arxiv.org/abs/2206.07271 . Cornell and Stanford.

Method: six experiments with 4,600 participants judging whether self-presentations (professional profiles, hospitality host profiles, dating profiles) were written by people or by AI language models of the time; a computational analysis of which features drove their judgments; and an experiment showing the judgments could be steered.

Findings, from the abstract: participants "were unable to detect self-presentations generated by state-of-the-art AI language models" in professional, hospitality and dating contexts. A computational analysis of language features showed that people judged by intuitive but flawed heuristics, taking first-person pronouns, contractions and family topics as signs of human writing. The authors show experimentally that these heuristics make human judgment of AI-generated language predictable and open to manipulation, so that AI systems can produce text perceived as "more human than human". They propose "AI accents" as a remedy.

Limits: short personal texts; 2022 models.

For our prose: contractions and a personal voice make text seem human to readers, but only because readers guess wrongly. Our reason to use contractions is the ear, not the guess (inference).

### Porter and Machery, "AI-generated poetry is indistinguishable from human-written poetry and is rated more favorably" [Study]

Scientific Reports 14, November 2024: https://www.nature.com/articles/s41598-024-76900-1 (the publisher page refused my tool; read through https://theconversation.com/people-cant-tell-the-difference-between-human-and-ai-generated-poetry-new-study-243750 and search snippets). University of Pittsburgh.

Method: 696 participants, mostly with little experience of poetry, read poems by ten well-known poets from Geoffrey Chaucer to Dorothea Lasky and poems generated by ChatGPT in each poet's style, judged authorship and rated qualities.

Findings: participants identified the AI poems at 46.6%, below chance, and were more likely to judge AI poems human than real ones; they rated the AI poems higher on qualities such as rhythm and beauty, and that preference drove the misattribution. The commentary in The Conversation argues the AI poems won by sanding off "the more challenging elements: ambiguity, wordplay, linguistic complexity", and that participants read the human poems' complexity as incoherence (they complained the human poems "don't make sense").

Limits: non-expert raters; a genre where difficulty is the point.

For our prose: a general audience may like smooth AI prose better on first contact. That is an argument for clarity, which we want anyway, not for the model's defaults: the smoothing that pleased those readers is the loss of particulars (inference).

### Russell, Karpinska and Iyyer, "People who frequently use ChatGPT for writing tasks are accurate and robust detectors of AI-generated text" [Study]

ACL 2025: https://aclanthology.org/2025.acl-long.267/ ; preprint https://arxiv.org/abs/2501.15654 ; data https://github.com/jenna-russell/human_detectors .

Method: annotators were hired to read 300 non-fiction English articles, label each as human or AI (GPT-4o, Claude 3.5 Sonnet, o1-Pro), give a confidence and write a paragraph explaining the decision. Some annotators used LLMs for writing often ("experts"), some did not. Further rounds tested paraphrased articles and "humanised" ones: the researchers paid experts for their lists of clues, compiled a guidebook covering vocabulary, grammar, tone, introductions and conclusions, and prompted o1-Pro with it to write articles that would evade a detector.

Findings: non-experts averaged 56.7% true positives and 51.7% false positives, about chance. Experts reached 92.7% and 3.3%; a majority vote of five experts misclassified 1 of 300 articles, outperforming most commercial detectors. Paraphrasing left their rates "almost unchanged". On humanised o1-Pro articles the majority vote was still 100% right with 0% false positives, although individual experts ranged widely and confidence fell (15.1% of judgments at the lowest level; average confidence 4.21 of 5 on non-humanised o1-Pro articles against 4.39 to 4.48 in earlier rounds). Experts noticed humanised articles gave people titles such as "Dr." and "Prof." far more than human articles did, and that the structure stayed formulaic; the humanisation "did not remove the entirety of the 'AI signature'". The clue categories and their shares are in 1.1 above. Experts named *vibrant*, *crucial* and *significantly* among the AI vocabulary.

Limits: long non-fiction articles, where there is more to go on than in a 60-second script; five paid experts.

For our prose: this is the strongest evidence that a prompt built from a list of tells does not make AI text read as human to an attentive reader, and it lists what such readers look at. It also shows the cost of trying: the prompt introduced a new tell of its own.

### Mai, Bray, Davies and Griffin, "Warning: Humans cannot reliably detect speech deepfakes" [Study]

PLOS ONE 18(8), e0285333, 2 August 2023: https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0285333 . University College London.

Method: 529 people listened to genuine and synthesised speech in English and Mandarin and flagged the deepfakes; some were first given examples.

Findings: deepfakes were correctly identified 73% of the time, with no difference between the languages; familiarising listeners with examples helped only slightly.

Limits: 2023 synthesis; about the voice, not the words.

For our prose: if the narration is synthesised, a quarter or more of listeners will not notice the voice, but most will; the script is only part of what sounds human (inference).

### Toff and Simon, "'Or They Could Just Not Use It?': The Dilemma of AI Disclosure for Audience Trust in News" [Study]

The International Journal of Press/Politics, 2024 to 2025: https://journals.sagepub.com/doi/abs/10.1177/19401612241308697 ; open version https://ora.ox.ac.uk/objects/uuid:65830edf-2b12-41f6-98e3-5855de38dfdd . University of Minnesota and the Oxford Internet Institute.

Method: a survey experiment in the United States with real AI-generated news articles, some labelled as AI-generated. (The sample size and the full set of conditions were not in what I could read.)

Findings: audiences perceived news labelled as AI-generated as less trustworthy, even when they did not rate the articles as any less accurate or fair, and the disclosure lowered trust in the news organisation as well as in the article.

Limits: news, not science video; a label, not a style.

For our prose: being taken for AI costs trust independently of quality. That applies to a style that reads as AI as much as to a label (inference).

### Carruthers and Heim, study of NotebookLM podcasts, AI & Society 2026 [Study]

Reported by https://bioengineer.org/ai-podcasts-sound-human-but-miss-the-hidden-rhythm-of-real-conversation/ (a news write-up; I did not read the paper). University of Aberdeen and University of Edinburgh.

Method: about ten-minute excerpts of podcasts generated by Google's NotebookLM and of human podcasts on the same topics were annotated with acoustic analysis software for turn length, overlaps, backchannels and roles; a feature that lets a human join the AI conversation was also tested.

Findings: human hosts' turns averaged 22.3 seconds and guests' 49.4, against 11.3 and 14.8 for the AI hosts; human guests overlapped for over four seconds on average and hosts for over two, the AI for milliseconds; AI backchannels came after pauses, not during speech. The whole AI dialogue was scripted before it was voiced, with gaps and overlaps suppressed, and a live human disrupted it (transitions slowed, overlaps vanished, glitches switched voices).

Limits: second-hand; dialogue, not single-voice narration.

For our prose: the audible sign of a machine was even, tidy timing. For a single narrator the equivalent is even sentence lengths and an even cadence (inference).

## 2.E Why models write this way, and what diversity methods do

### West and Potts, "Base Models Beat Aligned Models at Randomness and Creativity" [Study]

Conference on Language Modeling (CoLM) 2025: https://arxiv.org/abs/2505.00047 ; https://openreview.net/forum?id=vqN8uom4A1 . Stanford and the University of British Columbia.

Method: base language models compared with their aligned (instruction-tuned and preference-trained) versions on tasks that need unpredictability: random number generation, mixed-strategy games (rock-paper-scissors, hide-and-seek) and creative writing.

Findings: base models outperform aligned ones on these tasks; aligned models narrow to distinct habits, such as preferring "7" when asked for a random number. The authors argue alignment should not be applied universally.

Limits: base models are hard to steer and rarely available through commercial APIs.

For our prose: the house style is a product of alignment, so it will not be removed by asking an aligned model to be "creative"; the narrowing is structural (inference).

### Zhang and colleagues, "Verbalized Sampling: How to Mitigate Mode Collapse and Unlock LLM Diversity" [Study, preprint]

arXiv, October 2025: https://arxiv.org/abs/2510.01171 ; ICML 2026 poster https://icml.cc/virtual/2026/poster/60489 .

Method: the authors trace mode collapse to "typicality bias in preference data, whereby annotators systematically favor familiar text", and propose a prompt that asks for several responses with their probabilities ("Generate 5 jokes about coffee and their corresponding probabilities") instead of one.

Findings: in creative writing, verbalized sampling raised diversity by 1.6 to 2.1 times over direct prompting, without loss of factual accuracy or safety; it also made simulated dialogue more human-like and synthetic data more varied.

Limits: diversity is not quality; a preprint.

For our prose: when a line matters (the opening, the turn, the last line), ask for several candidates that differ, and choose. The first answer is the most typical one by construction (inference).

### Castricato, Lile, Anand, Schoelkopf, Verma and Biderman, "Suppressing Pink Elephants with Direct Principle Feedback" [Study, preprint]

arXiv, 12 February 2024: https://arxiv.org/abs/2402.07896 . EleutherAI and collaborators.

Method: the "pink elephant problem" is instructing a model to avoid discussing one entity and to discuss a preferred one instead. The authors fine-tuned Llama 2 13B with Direct Principle Feedback (DPO on critiques and revisions) and compared it with Llama-2-13B-Chat, a prompted baseline and GPT-4.

Findings: the fine-tuned model significantly outperformed the chat model and the prompted baseline and matched GPT-4 on their test set; prompting alone did not reliably keep the model off the named subject.

Limits: topic avoidance, not style; 2024 models.

For our prose: naming a phrase in a prompt to forbid it is a weak control; the evidence favours instructions that name the wanted behaviour, plus a check afterwards (inference).

### Wang, Tripto, Park, Li and Zhou, "Catch Me If You Can? Not Yet: LLMs Still Struggle to Imitate the Implicit Writing Styles of Everyday Authors" [Study]

Findings of EMNLP 2025; preprint 18 September 2025: https://arxiv.org/abs/2509.14543 ; https://aclanthology.org/2025.findings-emnlp.532.pdf .

Method: writing samples from over 400 real authors in news, email, forums and blogs; models asked to write new text in each author's style from a few examples; over 40,000 generations per model; judged by an ensemble of authorship attribution, authorship verification, style matching and AI detection.

Findings: models approximate style in structured formats (news, email) but struggle with informal writing (blogs, forums); few-shot examples bring outputs closer than zero-shot but they stay "significantly more distant than human writing"; returns diminish beyond four or five examples.

Limits: imitation of individuals, not of a house style.

For our prose: three to five good examples of the narration we want are worth including, and more are not; examples alone will not make the model write like a person, so the edit pass stays necessary (inference).

## 2.F Model makers' guidance

### Anthropic, "Prompting best practices" [Vendor]

https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/claude-prompting-best-practices (read 2026-09-26; covers current Claude models).

What it says, in its words. On reasons: "Providing context or motivation behind your instructions, such as explaining to Claude why such behavior is important, can help Claude better understand your goals and deliver more targeted responses." Its example: "NEVER use ellipses" is less effective than "Your response will be read aloud by a text-to-speech engine, so never use ellipses since the text-to-speech engine will not know how to pronounce them." "Claude is smart enough to generalize from the explanation." On examples: "Examples are one of the most reliable ways to steer Claude's output format, tone, and structure"; make them relevant, diverse ("vary enough that Claude doesn't pick up unintended patterns") and structured in example tags; "Include 3-5 examples for best results." On format: "Tell Claude what to do instead of what not to do" (instead of "Do not use markdown in your response", try "Your response should be composed of smoothly flowing prose paragraphs"); "Match your prompt style to the desired output ... removing markdown from your prompt can reduce the volume of markdown in the output"; and a sample instruction against bullet points and bold that asks for "clear, flowing prose using complete paragraphs and sentences". For front-end design it names the model's pull toward generic output directly: "You tend to converge toward generic, 'on distribution' outputs", the "AI slop" aesthetic.

Limits: vendor guidance with no published evaluation.

For our prose: write the narration brief itself in plain spoken sentences, give the reason for each rule (most of ours have one: the listener cannot re-read; the speech engine will say it aloud), and include three to five real examples that differ from each other.

### Anthropic, "Prompting Claude Fable 5.1", section "Writing density" [Vendor]

https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-fable-5-1 (read 2026-09-26).

What it says: the model's writing "has few stock phrases and little unexplained jargon", but in some cases its prose is denser than its predecessor's, with longer sentences and fewer paragraph breaks, and "An instruction that defines the anti-pattern, mannered prose, helps". The suggested instruction defines mannered prose as metaphor and flourish in place of direct statement, with paired examples: "a dial worth turning" for "a parameter worth varying", and "this point earns its keep" for "this point still matters". It gives the reasons: such phrases display the writer rather than convey the idea, they make the reader work harder, and a metaphor brings connotations the writer did not choose. Its fix is to say what you mean and to use the literal phrase whenever one is available. It adds that "Please remove all mannered prose" also tends to work.

Limits: vendor guidance, untested in public.

For our prose: this instruction does three things the evidence supports at once: it names the behaviour wanted (the literal phrase), gives the reason, and shows a paired example of each. It is a model for how to phrase every rule in a narration brief (inference).

## 2.G Writers' and editors' advice

### George Orwell, "Politics and the English Language" (1946) [Practitioner]

Summarised with the rules at https://sites.duke.edu/scientificwriting/orwells-6-rules/ and https://en.wikipedia.org/wiki/Politics_and_the_English_Language .

The six rules, in short: never use a figure of speech you are used to seeing in print; never use a long word where a short one will do; "If it is possible to cut a word out, always cut it out"; never use the passive where you can use the active; never use a foreign phrase, a scientific word or a jargon word if you can think of an everyday English equivalent; and break any of these rules "sooner than say anything outright barbarous". Orwell wrote about political prose, stale phrases standing in for thought; he did not follow the rules slavishly and said he was not writing about literary language.

Limits: opinion, and the passive rule is disputed by linguists (and Reinhart found GPT-4o uses agentless passives at half the human rate, so the passive is not the model's vice).

For our prose: rules (i) and (v) are the two that bite on AI narration: a stale figure is the model's most typical choice, and "scientific words" need an everyday equivalent for a general audience. Rule (iv) should be read as "say who did it", which also matters for the ear.

### William Strunk Jr., The Elements of Style (1918 and 1920; revised by E. B. White in 1959) [Practitioner]

Public-domain text at https://www.gutenberg.org/ebooks/37134 ; rule 16 quoted in many places.

Rule 16 in the Strunk and White editions (rule 12 in Strunk's 1918 text): "Use definite, specific, concrete language. Prefer the specific to the general, the definite to the vague, the concrete to the abstract." The gloss: "The greatest writers ... are effective largely because they deal in particulars and report the details that matter. Their words call up pictures." The next rule is "Omit needless words." Two neighbouring rules of the 1918 text bear on us too (quoted from my knowledge of the book, not from a page read today): "Put statements in positive form", which is the plain alternative to the "not X, it's Y" habit, and "Place the emphatic words of a sentence at the end", the position the editorial "-ing" tail wastes.

Limits: prescriptive; some of its grammar rules are dated.

For our prose: the most consistent point across every source in this document, and the one with the most indirect evidence behind it (LAMP's specificity edits, Wikipedia's regression to the mean, Chiang's average of choices).

### William Zinsser, On Writing Well (1976, many editions) [Practitioner]

Quoted at https://www.goodreads.com/quotes/8729114-but-the-secret-of-good-writing-is-to-strip-every and in the summaries at https://calvinrosser.com/notes/on-writing-well-william-zinsser/ .

In his words: "Clutter is the disease of American writing." The secret of good writing, he says, is "to strip every sentence to its cleanest components": every word that serves no function, every long word that could be short, every adverb that repeats what the verb already means, and every passive that leaves the reader unsure who is doing what weakens a sentence, and he adds that such clutter grows with education and rank. "Clear thinking becomes clear writing; one can't exist without the other." He suggested bracketing every part of a draft that does no work, and held that most first drafts can lose half their words without losing information or voice.

For our prose: the bracket test is a practical edit pass. Most AI tells (significance claims, "-ing" tails, closing summaries) are exactly the parts that do no work.

### Gary Provost, 100 Ways to Improve Your Writing (1985) [Practitioner]

Quoted at https://www.aerogrammestudio.com/2014/08/05/this-sentence-has-five-words/ .

The passage opens: "This sentence has five words. Here are five more words. Five-word sentences are fine. But several together become monotonous." It turns: "I vary the sentence length, and I create music. Music. The writing sings. It has a pleasant rhythm, a lilt, a harmony." And it closes: "And sometimes, when I am certain the reader is rested, I will engage him with a sentence of considerable length, a sentence that burns with energy and builds with all the impetus of a crescendo, the roll of the drums, the crash of the cymbals, sounds that say listen to this, it is important." (These three pieces were checked against the quoting pages; the lines between them were not, so they are left out. The passage is also quoted in Roy Peter Clark's Writing Tools.)

For our prose: Provost writes explicitly about the ear, which is our medium. The measured sentence-length variance of human writing (Desaire, Munoz-Ortiz) is the same point in numbers. Note that a run of very short sentences is as monotonous as a run of long ones; the fix is variety, not shortness.

### Steven Pinker, The Sense of Style (2014) [Practitioner]

Described at https://www.psychologicalscience.org/observer/the-curse-of-knowledge-pinker-describes-a-key-cause-of-bad-writing and in the book's chapters "A Window onto the World" and "The Curse of Knowledge".

Classic style: "prose is a window onto the world"; the writer has seen something true and directs the reader's gaze to it, as an equal in conversation. The curse of knowledge, "a difficulty in imagining what it is like for someone else not to know something that you know", is "the single best explanation I know of why good people write bad prose".

For our prose: our viewers do not know what a genome, a clade or a founder is; the narrator must show them what happened rather than name it. Classic style is also a stance, which is what the model lacks: the narrator saw this and is telling you (inference).

## 2.H Science storytelling

### Dahlstrom, "Using narratives and storytelling to communicate science with nonexpert audiences" [Study, review]

PNAS 111 (Supplement 4), 13614 to 13620, 2014: https://www.pnas.org/doi/10.1073/pnas.1320645111 ; https://pubmed.ncbi.nlm.nih.gov/25225368/ . Iowa State University.

What it is: a review of research on narrative in science communication for the Sackler colloquium on the science of science communication.

Findings: narratives "offer increased comprehension, interest, and engagement"; non-experts get most science from mass media, which already favour narrative; narratives are intrinsically persuasive, which helps with resistant audiences but raises ethical questions.

For our prose: tell an experiment as something that happened, in order, with a cause, not as a list of findings. The ethical point binds too: the story must not claim more than the run showed.

### Hillier, Kelly and Klinger, "Narrative Style Influences Citation Frequency in Climate Change Science" [Study]

PLOS ONE 11(12): e0167983, December 2016: https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0167983 ; https://www.ncbi.nlm.nih.gov/pmc/articles/PMC5158318/ .

Method: 732 abstracts from climate-change research scored on measures of narrativity drawn from psychology and literary theory, compared with citation counts.

Findings: more narrative abstracts were cited more; the effect is closely tied to journal identity (high-impact journals publish more narrative articles).

Limits: correlational and confounded with journal.

For our prose: modest support for narrative as the frame, among experts as well as lay readers.

### Randy Olson, the ABT ("And, But, Therefore") framework [Practitioner]

Olson is a marine biologist turned filmmaker; the framework is in Houston, We Have a Narrative (2015) and later books, which I did not read for this document (the "And, And, And" and "Despite, However, Yet" failure modes below are from my knowledge of those books, not from a page read today). On his "narrative index": https://abtagenda.substack.com/p/the-two-narrative-metrics ; workshops at https://research.washu.edu/events/event/the-abt-and-but-therefore-narrative-framework-training-with-randy-olson-phd/ .

What it says: a story has the shape "X and Y, but Z, therefore W": set-up, the problem or surprise, the consequence. Its failures are "And, And, And" (a list with no turn) and "Despite, However, Yet" (too many turns). The "narrative index" is the ratio of *but* to *and* in a text.

Limits: practitioner heuristic; the index is his own, not validated in what I read.

For our prose: every experiment story needs one "but", a real one. The AI default is "and, and, and" with a tidy close, which is StoryScope's flat escalation in another vocabulary (inference).

### Muller and Sharma, research on misconceptions in science videos [Study]

"Tackling misconceptions in introductory physics using multimedia presentations", Proceedings of the Australian Conference on Science and Mathematics Education: https://openjournals.library.sydney.edu.au/IISME/article/view/6345 ; and Muller, Bewes, Sharma and Reimann, "Saying the wrong thing: improving learning with multimedia by including misconceptions", Journal of Computer Assisted Learning, 2008: https://onlinelibrary.wiley.com/doi/abs/10.1111/j.1365-2729.2007.00248.x . University of Sydney; Derek Muller later founded the Veritasium channel, and lists this work at https://www.veritasium.com/publications .

Method: more than a thousand physics students over three years watched one of four online multimedia treatments on Newton's first and second laws: a concise lecture-style exposition; an extended exposition; a "refutation" (the exposition with common misconceptions stated and refuted); and a "dialogue" between a student and a tutor covering the same material as the refutation.

Findings: the refutation and the dialogue produced the largest learning gains, effect sizes 0.79 and 0.83 against the exposition (as reported in the summaries I read); students with low prior knowledge gained most, and high-prior-knowledge students were not harmed. The explanation offered: misconceptions give students "a false sense of knowing, limiting the mental effort they invest in learning", and naming them makes students confront the inconsistency.

Limits: physics teaching, not entertainment video; the effect sizes come from secondary summaries.

For our prose: this is the evidence-backed use of "not X but Y". State the expectation that was really held (what the researcher predicted, what anyone would guess the creatures would do), then show what happened. That is different from the AI habit of correcting an expectation nobody held (inference).

## 2.I Writing for the ear, and words with pictures

### NPR Training, "The journey from print to radio storytelling: a guide for navigating a new landscape" [Practitioner]

https://www.npr.org/sections/npr-training/2025/05/30/g-s1-65814/the-journey-from-print-to-radio-storytelling-a-guide-for-navigating-a-new-landscape (the page timed out for my tool; what follows is from search-result extracts that quote it and from radio-writing guides returned with it). NPR's training site draws on Jonathan Kern's Sound Reporting: The NPR Guide to Audio Journalism and Production (University of Chicago Press).

What it says: radio words "whiz past the ear, with no opportunity to slow down or rewind"; a listener who misses a point or is confused by a complex sentence "can't rewind; they simply lose the thread". Statistics on radio distract, are easily misheard and rarely have time for context, so instead of "4,187 people attended" say "more than four thousand people attended", and if an exact number is essential, keep it short and give one number per sentence. Aim for one idea per sentence; a benchmark of 20 words or fewer per sentence appears in the radio-writing guides returned by the same search (I could not confirm it is NPR's own). "Write like you talk"; picture the script as a conversation. Attribution goes before the assertion, at the start of the sentence, to ground the listener. Use the active voice. Fragments and colloquialisms that people use in speech can and should appear in scripts.

Limits: news practice, not tested; my reading is second-hand.

For our prose: these are the rules the ear adds to the rules against AI tells. They pull in one direction with most of them (short words, active verbs, concrete nouns) and against one: short sentences, followed too uniformly, recreate the monotony the variance studies measure. One idea per sentence, with variety in length inside that, satisfies both (inference).

### Media Helping Media, radio news script guides [Practitioner]

https://mediahelpingmedia.org/basics/tips-for-writing-radio-news-scripts/ and https://mediahelpingmedia.org/quick-guides/writing-a-radio-news-script/ . A free training site written by former BBC journalists and trainers (read through search-result extracts).

What it says: write in simple, short sentences so the meaning is clear at once; use everyday language the audience knows; use the active voice ("the police arrested the man", not "the man was arrested by the police"), which is "more energetic and easier to understand"; put the most important information in the first few sentences; think from the listener's side; one or two main ideas per sentence.

For our prose: the same rules as NPR's, from British practice.

### Fang, "The 'Easy listening formula'" [Study]

Journal of Broadcasting 11(1), 63 to 68, 1966: https://www.tandfonline.com/doi/abs/10.1080/08838156609363529 ; implementation notes at https://rdrr.io/cran/koRpus/man/ELF.html . From Irving Fang's UCLA dissertation, "A Computer-Based Analysis of Television News Writing Style", on an IBM research fellowship.

What it is: a listenability score for broadcast copy, based on the count of words with more than one syllable in a sentence (each syllable beyond the first scores a point), on the premise that listenability is not the same as readability.

Limits: 1966 television news; a formula, not a model of comprehension.

For our prose: long words cost more when heard than when read. Nominalisations (a model habit at twice the human rate) are long words by construction (inference).

### Center embedding and working memory [Study, summarised]

Summary at https://en.wikipedia.org/wiki/Center_embedding ; one research example, Frank and colleagues, "Cross-Linguistic Differences in Processing Double-Embedded Relative Clauses", Cognitive Science 2016: https://onlinelibrary.wiley.com/doi/full/10.1111/cogs.12247 .

What it says: sentences with a clause embedded between a subject and its verb, and above all with one embedding inside another, are hard to understand, because the listener must hold the unfinished parts in working memory until the verb arrives.

For our prose: a spoken sentence should put its subject and verb early and add the rest after them, rather than opening with a long subordinate clause or parking an aside between subject and verb. Dash asides do exactly that (inference).

### Drew and Grimes, "Audio-Visual Redundancy and TV News Recall" [Study]

Communication Research 14(4), 452 to 461, August 1987: https://journals.sagepub.com/doi/10.1177/009365087014004005 ; ERIC record https://eric.ed.gov/?id=EJ359196 .

Method: college students watched voice-over television news stories in three conditions: every story's pictures matched its narration; half matched; none matched.

Findings: auditory recall and story understanding were highest when narration and pictures were redundant; recall of the pictures showed the reverse pattern, higher when they did not match.

Limits: 1980s news; students.

For our prose: narration that talks about what is on screen at that moment is remembered and understood better. When the picture shows something the narration is not about, viewers attend to the picture and lose the words (inference from the reverse pattern).

### Adesope and Nesbit, "Verbal Redundancy in Multimedia Learning Environments: A Meta-Analysis" [Study]

Journal of Educational Psychology 104, 250 to 263, 2012: https://www.researchgate.net/publication/232469670_Verbal_Redundancy_in_Multimedia_Learning_Environments_A_Meta-Analysis ; https://www.semanticscholar.org/paper/Verbal-redundancy-in-multimedia-learning-A-Adesope-Nesbit/f3cf19bfea0d7d51f6914cb61a7f89f49d7666dc . Nesbit is at Simon Fraser University. Read through abstracts and search extracts; effect sizes were not in what I saw.

Method: a meta-analysis of 57 independent studies comparing spoken-only, written-only and spoken-plus-written presentations on retention and transfer.

Findings: spoken-plus-written beat spoken-only overall, and did not differ from written-only; the advantage held for learners with little prior knowledge, for system-paced materials and for materials without pictures; presentations that showed key terms extracted from the narration were associated with better learning and accounted for much of the advantage. The redundancy effect is small and depends on pacing, learners and design.

For our prose: in a system-paced video with pictures, the on-screen text that helps is a few key terms, not a transcript (inference, combining this with the next entry).

### The redundancy principle: Mayer; Clark and Mayer; Yue, Bjork and Bjork [Study, Practitioner]

Clark and Mayer, "Applying the Redundancy Principle: Explain Visuals with Words in Audio or Text but Not Both", in e-Learning and the Science of Instruction (Wiley): https://onlinelibrary.wiley.com/doi/abs/10.1002/9781119239086.ch7 [Practitioner, from research]. Yue, Bjork and Bjork, "Reducing verbal redundancy in multimedia learning: An undesired desirable difficulty?", Journal of Educational Psychology 2013: https://bjorklab.psych.ucla.edu/wp-content/uploads/sites/13/2016/07/YueBjorkBjork2013_redundancy.pdf [Study]. A 2026 re-examination, "A redundant redundancy principle? Examining the redundancy principle through cognitive load", Educational Psychology: https://www.tandfonline.com/doi/full/10.1080/01443410.2026.2678483 (the page refused my tool; title only).

What they say: when narration explains an animation or picture, adding the same words as on-screen text generally impairs learning; brief labels inside the animation can improve it, as earlier research found; Yue, Bjork and Bjork found impairment for on-screen text identical to the narration in both of their experiments. Learners, notably, misjudge this: a study titled "Learners misperceive the benefits of redundant text in multimedia learning" (https://pubmed.ncbi.nlm.nih.gov/25071674/, seen as a search result only) reports that they believe the redundant text helps.

For our prose: the on-screen text should be short labels and key terms that point at the picture (a name, a count, a time), not the narration's sentences. Captions for viewers watching with the sound off are a different job, accessibility, and follow the narration exactly (inference).

---

# Part 3. Tells to avoid

Words and phrases:

- Significance and praise: *testament, pivotal, crucial, vital role, key turning point, setting the stage, remarkable, fascinating, groundbreaking, profound, vibrant, rich, intricate, meticulous, tapestry*.
- Connecting and appraising verbs: *delve, underscore, highlight, showcase, emphasize, foster, enhance, bolster, garner, align with, resonate with*.
- Copula dodges: *serves as, stands as, marks, represents, boasts, features* where *is* or *has* would do.
- Vague agents: *experts, researchers, observers, some say, scientists found* without saying who and what.
- Fake subtlety and stock sensation: *quiet, subtle, shadowy, whisper, hum, woven, a sense of*, and every figure of speech you have seen before.
- Hype openers and interjections: a question answered at once ("The result?"), "And honestly?", "Here's the thing".

Sentence patterns:

- "It's not X, it's Y" and "not just X, but Y", unless X is an expectation someone really held.
- Lists of three by reflex: three adjectives, three parallel phrases.
- A closing "-ing" clause that says what the sentence means: ", revealing ...", ", highlighting ...", ", suggesting ...".
- Nominalisations and noun stacks: "the emergence of cooperation", "population-level dynamics".
- Runs of sentences of the same length, long or short.
- Asides between subject and verb, and dash asides generally.

Structure and tone:

- A summary, moral or uplifting close ("a reminder that ...", "and that changes everything").
- Stating the theme the story has already shown.
- A tidy single track where the run had loose ends; flat escalation.
- Uniform positivity; no stance, no surprise, no disappointment.
- On screen: em dashes, title case, bold, emoji, the narration's sentences repeated as text.

---

# Part 4. The ten most useful, best-supported rules for our prose

Each rule states its evidence and how strong it is.

**1. Name the particular thing that happened.** Say which creatures, how many, where in the tank, at what second, and what they did, in words that only this experiment could supply. Evidence: professional editors of LLM prose kept adding specificity even after the cliches were gone (LAMP); Wikipedia's editors see models replacing specific facts with generic description; lack of originality was a quarter of expert detectors' clues (Russell et al.); Strunk, Orwell and Chiang say the same from practice. Strength: strong convergence of studies and practice.

**2. State what happened; do not appraise it.** Cut every word whose job is to say that something matters, impresses or reveals. Let the event carry its own weight. Evidence: the measured excess vocabulary is appraising verbs and adjectives (Kobak et al.; Reinhart et al.); vocabulary was the first clue of expert detectors (53.1%, Russell et al.); Wikipedia's significance and puffery categories; being taken for AI costs trust (Toff and Simon). Strength: strong.

**3. End each sentence on its fact, not on a comment about it.** No trailing "-ing" clause that interprets. If the meaning needs saying, it gets its own sentence, with a subject who holds the view. Evidence: GPT-4o uses present participial clauses at 5.3 times the human rate (Reinhart et al.); Wikipedia's "superficial analysis"; the end of a sentence is its emphatic position (Strunk). Strength: strong, measured.

**4. Use plain verbs, "is" and short words.** Turn nominalisations back into verbs ("the population collapsed", not "the collapse of the population"); prefer *is* and *has* to *serves as* and *boasts*; prefer the one-syllable word. Evidence: nominalisations at about twice the human rate (Reinhart; Herbold); copula avoidance (Wikipedia); long words are harder to hear (Fang); Orwell and Zinsser. Strength: strong on the page, practitioner-backed for the ear.

**5. Ration the rhetorical figures.** Use "not X but Y" at most once in a script, and only when X is a real expectation: what the researcher predicted, what a viewer would guess. Do not group things in threes by habit. Do not pose a question to answer it in the next breath. Evidence: the corrective figure at 6.3 times the human rate in some models (Antislop) and 2.2 times in speech-like writing on Claude models (Boggia, weak); sentence structure was the second clue of expert detectors (35.9%); Wikipedia and Kriss on threes; refuting a real misconception improves learning (Muller and Sharma). Strength: moderate; the counts are from preprints.

**6. Vary sentence length on purpose, inside what the ear can take.** One idea per sentence, most sentences short enough to say in a breath (about 20 words is the broadcast ceiling), some much shorter, and now and then a longer one that runs forward without nesting. Never three of the same length in a row. Evidence: human writers vary sentence length more and write more very short and very long sentences (Desaire et al.; Munoz-Ortiz et al.); Provost; NPR and BBC practice for the ear. Strength: strong for variance; practitioner for the ceiling.

**7. Build the story on its turn.** What was expected, what happened instead, what followed from it: one real "but" and a "therefore". Keep a second thread or a loose end if the run had one. Evidence: narrative raises comprehension and engagement (Dahlstrom) and citation (Hillier et al., correlational); stating and overturning an expectation teaches better (Muller and Sharma); AI stories are tidy, single-track and flat (StoryScope); Olson's ABT. Strength: moderate to strong.

**8. Stop when the story stops.** End on the last event, the last number or the open question. No summary, moral, reassurance or uplift, and no statement of the theme. Evidence: tidy optimistic conclusions were an expert clue (13.1%, Russell et al.); Wikipedia's outline-like conclusions; AI fiction over-explains its themes (StoryScope); a fifth of expert edits cut redundant exposition (LAMP); human text carries more negative emotion (Munoz-Ortiz et al.). Strength: moderate to strong.

**9. Write for one hearing.** Subject and verb early; who said or measured something before what they found; one number per sentence, rounded unless the exact figure is the point; the same name for the same thing every time; contractions; no aside between subject and verb, and dash asides rewritten as sentences of their own. Evidence: NPR and BBC-trained practice; center-embedding research on working memory; Fang's listenability; Anthropic's note that a speech engine cannot pronounce some punctuation; the em dash as a current AI marker (Freeburg; Czuma). Strength: practitioner consensus with basic psycholinguistic support.

**10. Make the words and the picture one message.** Narrate what is on screen at that moment. Keep on-screen text to short labels and key terms that point at the picture, never the narration's sentences; captions for silent viewing are a separate, exact transcript. Evidence: narration that matches the pictures is recalled and understood better (Drew and Grimes); identical on-screen text impairs learning while key terms and labels help (Adesope and Nesbit's meta-analysis; Yue, Bjork and Bjork; Clark and Mayer). Strength: moderate; the evidence is from learning, not entertainment.

---

# Part 5. A working process, and the evidence for each step

This is the process the evidence supports when a model drafts the narration. Each step names its support; where the support is inference, it says so.

**Brief the writer positively, with reasons.** Phrase each rule as what to do ("end each sentence on the fact"), give its reason ("a listener hears the end of a sentence loudest and cannot re-read it"), and write the brief itself in the plain spoken register wanted, without bullets or bold if the output should have none. Support: Anthropic's guidance [Vendor]; the pink-elephant study on prohibitions [Study, preprint].

**Name the two or three worst habits directly, in one line each.** A one-line instruction to state things affirmatively cut the corrective figure by about 70% on Claude models (Boggia [Study, weak]); Anthropic's "mannered prose" definition is the model for the wording: the pattern, a paired example of it and its literal version, and the reason [Vendor]. Keep the list short; long lists of banned phrases have not been shown to work in a prompt (Russell et al.'s humaniser [Study]; Antislop on token banning [Study, preprint]).

**Give three to five example scripts that differ from each other.** Support: Anthropic [Vendor]; Wang et al.'s plateau at four or five examples [Study]. Choose examples written by people, and vary them so the model copies the manner and not one script's shape.

**Draft several versions of the lines that matter.** Opening, the turn, the last line. Ask for several distinct candidates and choose, by a person or by a specific question ("which names a thing that happened?"), not by asking a model which is better written, since general models judge writing quality little better than chance (Chakrabarty, Laban and Wu 2025 [Study, preprint]). Support: verbalized sampling's diversity gains (Zhang et al. [Study, preprint]); the first answer of an aligned model is its most typical (West and Potts [Study]) (inference for the application).

**Run a mechanical check.** Scan the draft for the Part 3 words and patterns, sentence-length runs, trailing "-ing" clauses, em dashes and more than one number in a sentence, and send any hit back for a rewrite of the whole sentence, not a synonym swap. Support: the Antislop pipeline profiles and checks patterns rather than trusting the prompt [Study, preprint]; Shaib et al. show the sameness is in sentence shapes, which a synonym swap keeps [Study]; OpenAI's and Freeburg's evidence that even a single banned character leaks through instructions [News; Measurement].

**Edit with the LAMP taxonomy, by a separate reader.** Look for cliche, unnecessary exposition, purple prose, poor sentence structure, lack of specificity, awkward word choice and tense slips, and cut before adding. Support: LAMP's preference ranking of writer-edited over LLM-edited over unedited text [Study]; Zinsser's bracket test [Practitioner]. A human editor is better than a model editor by LAMP's result; a model editor is better than none.

**Check the facts after every edit.** Numbers, names, times and claims against the source. Support: Abdulhai et al. found LLM editors change meaning even when told to fix grammar only [Study, preprint] (inference for the application).

**Listen to it.** Read the script aloud, or render it through the speech engine that will voice it, and mark every place where the voice stumbles, pauses oddly or drones. Support: broadcast practice [Practitioner]; Anthropic's note on punctuation a speech engine cannot say [Vendor]; the evenness of machine timing (Carruthers and Heim [Study, as reported]) (inference for the application).

**Vary the shape across videos.** A series drafted by one model converges; change the opening move, the order of events and the structure from one video to the next. Support: Doshi and Hauser's rise in similarity [Study]; StoryScope's clustering of AI stories [Study, preprint] (inference for the application).

---

# Part 6. What I looked for and did not find

No study I found tests whether listeners can tell LLM-written narration scripts from human ones, or which tells they hear; Part 1.5's ranking is inference. I found no count of tricolons (lists of three) in LLM text against human text, although every guide names them. I found no study that measures whether banning words in a prompt makes a model substitute near-synonyms or produce new tells; the one hint is Russell et al.'s observation that the humanised articles overused honorifics. I found no study of how speech engines render dashes, colons and ellipses. Several sources could be read only through abstracts, summaries or search extracts because the publisher refused the fetching tool: Doshi and Hauser, Porter and Machery, Toff and Simon, Adesope and Nesbit, Drew and Grimes, Muller et al. 2008, the 2026 redundancy paper, Sam Kriss's essay and the NPR training page. Their numbers here come from those extracts. Commercial detector vendors' explanations of "perplexity" and "burstiness" appeared in searches but were not used, since they are marketing; Desaire et al. and Munoz-Ortiz et al. measure the same thing in the open.

---

# Part 7. Source table

| Source | Type | URL | How it was read |
|---|---|---|---|
| Wikipedia, Signs of AI writing | Field guide | https://en.wikipedia.org/wiki/Wikipedia:Signs_of_AI_writing | Page and raw text through the fetch tool |
| WikiProject AI Cleanup | Field guide | https://en.wikipedia.org/wiki/Wikipedia:WikiProject_AI_Cleanup | Search extract |
| Kriss, Why Does A.I. Write Like ... That? (NYT Magazine 2025) | Essay | https://longreads.com/2025/12/04/why-does-a-i-write-like-that/ ; https://rogerwong.me/2026/01/why-does-ai-write-like-that | Excerpts in two secondary pages |
| Chiang, Why A.I. Isn't Going to Make Art (New Yorker 2024) | Essay | https://www.newyorker.com/culture/the-weekend-essay/why-ai-isnt-going-to-make-art | Quotations in search results and reviews |
| Kobak et al., excess vocabulary (Science Advances 2025) | Study | https://www.science.org/doi/10.1126/sciadv.adt3813 ; https://pmc.ncbi.nlm.nih.gov/articles/PMC12219543/ | Open full text through the fetch tool |
| Reinhart et al., Do LLMs write like humans? (PNAS 2025) | Study | https://www.pnas.org/doi/10.1073/pnas.2422455122 ; https://arxiv.org/abs/2410.16107 | arXiv full text through the fetch tool |
| Juzek and Ward, Why does ChatGPT delve so much? (COLING 2025) | Study | https://arxiv.org/abs/2412.11385 | Abstract and search extracts |
| Yakura et al., LLM influence on spoken communication | Study, preprint | https://arxiv.org/abs/2409.01754 ; https://zenodo.org/records/21298066 | Abstract page and project record |
| Desaire et al., distinguishing science writing from ChatGPT (2023) | Study | https://pmc.ncbi.nlm.nih.gov/articles/PMC10328544/ | Open full text through the fetch tool |
| Munoz-Ortiz et al., human and LLM news text (2024) | Study | https://arxiv.org/abs/2308.09067 | Abstract page |
| Herbold et al., human versus ChatGPT essays (2023) | Study | https://www.nature.com/articles/s41598-023-45644-9 ; https://pmc.ncbi.nlm.nih.gov/articles/PMC10616290/ | Search extracts |
| Shaib et al., syntactic templates (EMNLP 2024) | Study | https://arxiv.org/abs/2407.00211 | Search extracts |
| Shaib et al., Measuring AI slop (2025) | Study, preprint | https://arxiv.org/abs/2509.19163 | Search extracts |
| Paech and colleagues, Antislop (2025) | Study, preprint | https://arxiv.org/abs/2510.15061 | arXiv full text through the fetch tool |
| Boggia, Artificial Epanorthosis (2026) | Study, preprint | https://arxiv.org/abs/2607.21498 | arXiv full text through the fetch tool |
| Freeburg, The Last Fingerprint (2026) | Measurement, preprint | https://arxiv.org/abs/2603.27006 | Abstract page and search extracts |
| Czuma, Em-ergence of the em-dash (2026) | Measurement, preprint | https://arxiv.org/abs/2606.29540 | Abstract page |
| Altman on em dashes; PCWorld report (2025) | News | https://x.com/sama/status/1989193813043069219 ; https://www.pcworld.com/article/2977726/openai-has-fixed-chatgpts-infamous-em-dash-obsession.html | Search extracts |
| Chakrabarty et al., Art or Artifice? (CHI 2024) | Study | https://arxiv.org/abs/2309.14556 | Abstract page |
| Russell et al., StoryScope (2026) | Study, preprint | https://arxiv.org/abs/2604.03136 | Abstract page |
| Doshi and Hauser, GenAI and collective diversity (Science Advances 2024) | Study | https://www.science.org/doi/10.1126/sciadv.adn5290 ; https://www.sciencedaily.com/releases/2024/07/240712222127.htm | Press summary and search extracts |
| Chakrabarty et al., Can AI writing be salvaged? LAMP (CHI 2025) | Study | https://arxiv.org/abs/2409.14509 | arXiv full text through the fetch tool |
| Abdulhai et al., How LLMs distort our written language (2026) | Study, preprint | https://arxiv.org/abs/2603.18161 | Abstract page |
| Chakrabarty, Ginsburg and Dhillon, Readers prefer AI trained on copyrighted books (2025) | Study, preprint | https://arxiv.org/abs/2510.13939 | Abstract page |
| Chakrabarty, Laban and Wu, AI-Slop to AI-Polish? (2025) | Study, preprint | https://arxiv.org/abs/2504.07532 | Abstract page |
| Clark et al., All that's human is not gold (ACL 2021) | Study | https://aclanthology.org/2021.acl-long.565/ | Abstract page |
| Jakesch et al., Human heuristics for AI-generated language are flawed (PNAS 2023) | Study | https://www.pnas.org/doi/10.1073/pnas.2208839120 ; https://arxiv.org/abs/2206.07271 | Full abstract |
| Porter and Machery, AI poetry (Scientific Reports 2024) | Study | https://www.nature.com/articles/s41598-024-76900-1 | The Conversation commentary and search extracts |
| Russell et al., frequent ChatGPT users as detectors (ACL 2025) | Study | https://arxiv.org/abs/2501.15654 | arXiv full text through the fetch tool |
| Mai et al., speech deepfakes (PLOS ONE 2023) | Study | https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0285333 | Search extracts |
| Toff and Simon, AI disclosure and trust (IJPP) | Study | https://journals.sagepub.com/doi/abs/10.1177/19401612241308697 | Search extracts |
| Carruthers and Heim, NotebookLM podcasts (AI & Society 2026) | Study | https://bioengineer.org/ai-podcasts-sound-human-but-miss-the-hidden-rhythm-of-real-conversation/ | News write-up only |
| West and Potts, Base models beat aligned models (CoLM 2025) | Study | https://arxiv.org/abs/2505.00047 | Search extracts |
| Zhang et al., Verbalized sampling (2025) | Study, preprint | https://arxiv.org/abs/2510.01171 | Abstract page |
| Castricato et al., Suppressing pink elephants (2024) | Study, preprint | https://arxiv.org/abs/2402.07896 | Search extracts |
| Wang et al., Catch me if you can? (EMNLP Findings 2025) | Study | https://arxiv.org/abs/2509.14543 | Abstract page and search extracts |
| Anthropic, Prompting best practices | Vendor | https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/claude-prompting-best-practices | Full page |
| Anthropic, Prompting Claude Fable 5.1 (writing density) | Vendor | https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-fable-5-1 | Full page |
| Orwell, Politics and the English Language | Practitioner | https://sites.duke.edu/scientificwriting/orwells-6-rules/ | Search extracts |
| Strunk, The Elements of Style | Practitioner | https://www.gutenberg.org/ebooks/37134 | Search extracts; catalogue page |
| Zinsser, On Writing Well | Practitioner | https://www.goodreads.com/quotes/8729114-but-the-secret-of-good-writing-is-to-strip-every | Quotation page |
| Provost, 100 Ways to Improve Your Writing | Practitioner | https://www.aerogrammestudio.com/2014/08/05/this-sentence-has-five-words/ | Quotation pages and search extracts |
| Pinker, The Sense of Style | Practitioner | https://www.psychologicalscience.org/observer/the-curse-of-knowledge-pinker-describes-a-key-cause-of-bad-writing | Search extracts |
| Dahlstrom, narratives in science communication (PNAS 2014) | Study, review | https://www.pnas.org/doi/10.1073/pnas.1320645111 | Search extracts |
| Hillier et al., narrative style and citation (PLOS ONE 2016) | Study | https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0167983 | Search extracts |
| Olson, ABT framework | Practitioner | https://abtagenda.substack.com/p/the-two-narrative-metrics | Search extracts |
| Muller and Sharma; Muller et al. 2008, misconceptions in multimedia | Study | https://openjournals.library.sydney.edu.au/IISME/article/view/6345 ; https://onlinelibrary.wiley.com/doi/abs/10.1111/j.1365-2729.2007.00248.x | Abstract page and search extracts |
| NPR Training, from print to radio storytelling | Practitioner | https://www.npr.org/sections/npr-training/2025/05/30/g-s1-65814/the-journey-from-print-to-radio-storytelling-a-guide-for-navigating-a-new-landscape | Search extracts (page timed out) |
| Media Helping Media, radio news scripts | Practitioner | https://mediahelpingmedia.org/basics/tips-for-writing-radio-news-scripts/ | Search extracts |
| Fang, Easy Listening Formula (1966) | Study | https://www.tandfonline.com/doi/abs/10.1080/08838156609363529 | Search extracts |
| Center embedding (summary); Frank et al. 2016 | Study, summarised | https://en.wikipedia.org/wiki/Center_embedding ; https://onlinelibrary.wiley.com/doi/full/10.1111/cogs.12247 | Search extracts |
| Drew and Grimes, audio-visual redundancy (1987) | Study | https://journals.sagepub.com/doi/10.1177/009365087014004005 | Search extracts |
| Adesope and Nesbit, verbal redundancy meta-analysis (2012) | Study | https://www.researchgate.net/publication/232469670_Verbal_Redundancy_in_Multimedia_Learning_Environments_A_Meta-Analysis | Search extracts |
| Yue, Bjork and Bjork, reducing verbal redundancy (2013) | Study | https://bjorklab.psych.ucla.edu/wp-content/uploads/sites/13/2016/07/YueBjorkBjork2013_redundancy.pdf | Search extracts |
| Clark and Mayer, redundancy principle chapter | Practitioner | https://onlinelibrary.wiley.com/doi/abs/10.1002/9781119239086.ch7 | Search extracts |
| Learners misperceive the benefits of redundant text | Study | https://pubmed.ncbi.nlm.nih.gov/25071674/ | Search result title only |
| A redundant redundancy principle? (Educational Psychology 2026) | Study | https://www.tandfonline.com/doi/full/10.1080/01443410.2026.2678483 | Title only (page refused) |
