# AI Features (Frontend Guide)

All GraphQL operations are at `POST /gql`. Every AI field below is `[Authorize]` — attach
`Authorization: Bearer <accessToken>` to the request (same token as every other authenticated
query; see `AUTH_FRONTEND.md`).

There are three AI surfaces:

1. **`chat(input)`** — AI content writer (drafts an engaging post from a short idea).
2. **`getCaptions(postId)`** — caption suggestions for an existing post.
3. **Recommendations** — `getPostRecommendations`, `getReelRecommendations`,
   `getUserRecommendations` — personalized discovery.

## Shared rate limit (chat + captions)

- Each user gets **5 AI requests per day** (`AiUsage` table, reset daily server-side).
- The counter is **shared** between `chat` and `getCaptions` — not per feature.
- When exhausted the endpoint returns the literal string `AI usage limit reached (5 requests).`
  (and captions returns a single-element list with the same text). Show a friendly
  "Daily AI limit reached — try again tomorrow" message and hide/disable the AI buttons.
- Recommendations are **not** counted against this limit.

## 1. chat(input) — AI content writer

Not a general chatbot: it turns a title or a rough idea into a complete, ready-to-post social
media post (hook, short paragraphs, skimmable, no emoji spam).

```graphql
query Chat($input: String!) {
  chat(input: $input)
}
```

- Returns a plain string (or `null`).
- **Personalization:** the backend injects the logged-in user's `FullName` into the prompt, so
  the AI knows who it is chatting with and addresses them by name. No frontend work needed —
  just pass the raw user input.

UX suggestions:

- "AI Draft" button on the post composer → user types an idea → preview the generated post,
  allow editing, then publish.
- Rewrite/improve existing text by pasting it in.
- Generate hooks/headlines for posts and reels.
- Surface the shared 5/day limit near the button (see above).

## 2. getCaptions(postId) — caption suggestions

Suggests 3 short, catchy captions for a text/image/video post. For video posts it uses the
stored transcript.

```graphql
query GetCaptions($postId: UUID!) {
  getCaptions(postId: $postId)
}
```

- Returns `[String]` — 3 suggestions.
- The post must already exist (has an id) — captions are generated from the saved post.

UX suggestions:

- "Suggest captions" on the composer before publishing (the creator picks/edits one).
- Post-detail "make this shareable" — caption + hook in one tap.

## 3. Recommendations — personalized discovery

All three are personalized for the current user (ML + content vectors).

```graphql
query GetPostRecommendations($limit: Int) {
  getPostRecommendations(limit: $limit) {
    succeeded
    message
    errors
    data {
      id
      title
      content
      mediaUrl
      createdAt
      ...userFields
    }
  }
}
```

- `getPostRecommendations(limit: Int = 10)` → `ApiResponse<[PostDto]>`.
- `getReelRecommendations(limit: Int = 10)` → `ApiResponse<[ReelDto]>`.
- `getUserRecommendations` → Relay paginated list of `UserType` (use connection args
  `first`/`after`).

UX suggestions:

- "For you" feed on the home screen (mix posts + reels).
- "More like this" module under a post/reel.
- "People you may know" sidebar and first-login follow suggestions.

## Handling errors

- Auth failures behave like every other authenticated call (401 / not authenticated).
- `ApiResponse` fields: `succeeded`, `message`, `errors`, `data`. Recommendations check
  `succeeded` before rendering `data`.
- `chat` / `getCaptions` return raw values — guard against the rate-limit string and `null`.
