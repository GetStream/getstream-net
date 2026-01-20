# Feeds V2 to V3 SDK Migration Guide

## Overview
- **V2 SDK**: `stream-net`
- **V3 SDK**: `getstream-net`

## Critical Breaking Changes

### 1. Response Structure
**V2:** Returns objects directly
```
var activity = await feed.AddActivityAsync(activity);
// activity is Activity object
```

**V3:** Wrapped in `StreamResponse<T>`
```
var response = await client.AddActivityAsync(request);
var activity = response.Data.Activity;  // Access via .Data property
```

### 2. Exception Types
**V2:** `StreamException`
**V3:** `GetStreamApiException` (with `StatusCode` and `ResponseBody` properties)

### 3. Activity Model Structure
**V2:** Uses `Actor`, `Verb`, `Object` pattern
```
var activity = new Activity("user:123", "post", "article:456");
// Object field is a string reference (e.g., "article:456")
// Some customers store custom_data inside Object as JSON string
activity.SetData("custom_field", "value");  // Custom data via SetData
```

**V3:** Uses `UserID`, `Type`, `Text` pattern
```
var request = new AddActivityRequest {
    UserID = "123",
    Type = "post",
    Text = "Hello world",
    Feeds = new List<string> { "user:123" },
    Custom = new { custom_field = "value" },  // Use custom field
    FilterTags = new List<string> { "tag1" },  // For filtering
    SearchData = new { ... }  // For searching
};
// No Object field - use filter_tags/search_data for filtering/searching
// Use custom field for custom data (not in Object)
```

### 4. Pagination
**V2:** Uses `offset` parameter
```
var result = await feed.GetActivitiesAsync(offset: 20, limit: 10);
var nextPage = await feed.GetActivitiesAsync(offset: 30, limit: 10);
```

**V3:** Uses `next`/`prev` tokens
```
var result = await client.QueryActivitiesAsync(new QueryActivitiesRequest {
    limit = 10
});
var nextPage = await client.QueryActivitiesAsync(new QueryActivitiesRequest {
    next = result.Data.Next,  // Token from previous response
    limit = 10
});
```

### 5. Feed Context
**V2:** Implicit (method called on feed instance)
```
var feed = client.Feed("news", "123");
await feed.AddActivityAsync(activity);  // Feed context is implicit
```

**V3:** Explicit (feed ID in request)
```
await client.AddActivityAsync(new AddActivityRequest {
    Feeds = new List<string> { "news:123" },  // Must specify feed(s)
    // ... other properties
});
```

## Common Operations

### Add Activity
**V2:**
```
var feed = client.Feed("user", "123");
var activity = new Activity("user:123", "post", "article:456");
var result = await feed.AddActivityAsync(activity);
```

**V3:**
```
var request = new AddActivityRequest {
    UserID = "123",
    Type = "post",
    Text = "Hello",
    Feeds = new List<string> { "user:123" }
};
var response = await client.AddActivityAsync(request);
var activity = response.Data.Activity;
```

### Get Feed Activities
**V2:**
```
var feed = client.Feed("news", "123");
var result = await feed.GetActivitiesAsync(offset: 0, limit: 20);
var activities = result.Results;
```

**V3:**
```
var response = await client.QueryActivitiesAsync(new QueryActivitiesRequest {
    filter = new { feed_id = "news:123" },
    limit = 20
});
var activities = response.Data.Activities;
```

### Follow Feed
**V2:**
```
var feed = client.Feed("user", "123");
await feed.FollowFeedAsync("timeline", "456");
```

**V3:**
```
await client.FollowAsync(new FollowRequest {
    Source = "user:123",
    Target = "timeline:456"
});
```

### Get Following
**V2:**
```
var feed = client.Feed("user", "123");
var result = await feed.FollowingAsync(offset: 0, limit: 25);
```

**V3:**
```
var response = await client.QueryFollowsAsync(new QueryFollowsRequest {
    filter = new { source = "user:123" },
    limit = 25
});
var following = response.Data.Follows;
```

### Update Activity Targets
**V2:** Incremental add/remove
```
await feed.UpdateActivityToTargetsAsync(activityId,
    adds: new[] { "feed1:123" },
    removed: new[] { "feed2:456" });
```

**V3:** Replace all feeds (must combine existing + adds - removed)
```
// First get current feeds, then combine
await client.UpdateActivityAsync(activityId, new UpdateActivityRequest {
    feeds = new[] { "feed1:123", "feed3:789" }  // Complete list
});
```

### Reactions
**V2:** Separate reactions service
```
var reaction = await client.Reactions.AddAsync("like", activityId, userId);
var reactions = await client.Reactions.FilterAsync(filtering, pagination);
```

**V3:** Reactions tied to activities/comments
```
// Activity reactions
await client.AddActivityReactionAsync(activityId, new AddReactionRequest {
    Type = "like",
    UserID = userId
});
var reactions = await client.QueryActivityReactionsAsync(activityId, request);

// Comment reactions (NEW in V3)
await client.AddCommentReactionAsync(commentId, new AddCommentReactionRequest {
    Type = "like",
    UserID = userId
});
```

### Comments
**V2:** Not available - comments don't exist

**V3:** First-class comments feature
```
// Add comment
await client.AddCommentAsync(new AddCommentRequest {
    Comment = "Great post!",
    ObjectID = activityId,
    ObjectType = "activity",
    UserID = userId
});

// Query comments
var response = await client.QueryCommentsAsync(new QueryCommentsRequest {
    filter = new { object_id = activityId }
});
```

### Polls
**V2:** Not available - polls don't exist

**V3:** Polls feature available
```
// Cast vote
await client.CastPollVoteAsync(activityId, pollId, new CastPollVoteRequest {
    UserID = userId,
    Vote = new VoteData { ... }
});

// Delete vote
await client.DeletePollVoteAsync(activityId, pollId, voteId);
```

### Enrich Activities
**V2:**
```
var enriched = await batchOps.GetEnrichedActivitiesAsync(activityIds, options);
```

**V3:**
```
var response = await client.QueryActivitiesAsync(new QueryActivitiesRequest {
    filter = new { id = new[] { "id1", "id2" } },
    // Add enrichment options to request
});
```

### Object Field and Custom Data
**V2:** `Object` field stores string reference, custom data via `SetData()`
```
var activity = new Activity("user:123", "post", "article:456");
activity.SetData("category", "tech");  // Custom data
// Some customers store JSON in Object field itself
```

**V3:** No `Object` field, use separate fields
```
var request = new AddActivityRequest {
    Custom = new { category = "tech" },  // Custom data here
    FilterTags = new List<string> { "tech", "news" },  // For filtering
    SearchData = new { category = "tech", author = "john" }  // For searching
};
```

## Function Mappings

| V2 SDK | V3 SDK | Notes |
|--------|--------|-------|
| `client.Feed(slug, id)` | `client.Feed(feedGroup, feedId)` | Same |
| `feed.AddActivityAsync()` | `client.AddActivityAsync(request)` | Request must include `feeds` |
| `feed.GetActivitiesAsync()` | `client.QueryActivitiesAsync(request)` | Use `filter.feed_id` |
| `feed.FollowFeedAsync(target)` | `client.FollowAsync(request)` | Source explicit in request |
| `feed.UnfollowFeedAsync()` | `client.UnfollowAsync(source, target)` | Both required |
| `feed.FollowingAsync()` | `client.QueryFollowsAsync(request)` | Use `filter.source` |
| `feed.FollowersAsync()` | `client.QueryFollowsAsync(request)` | Use `filter.target` |
| `feed.UpdateActivityAsync()` | `client.UpdateActivityAsync(id, request)` | Direct update |
| `feed.RemoveActivityAsync()` | `client.DeleteActivityAsync(id)` | Renamed |
| `feed.AddActivitiesAsync()` | `client.UpsertActivitiesAsync(request)` | Batch upsert |
| `feed.UpdateActivityToTargetsAsync()` | `client.UpdateActivityAsync(id, request)` | Replace all feeds |
| `client.Reactions.AddAsync()` | `client.AddActivityReactionAsync()` / `AddCommentReactionAsync()` | Reactions tied to activities/comments |
| `client.Reactions.FilterAsync()` | `client.QueryActivityReactionsAsync()` / `QueryCommentReactionsAsync()` | Query-based |
| Comments | `client.AddCommentAsync()` / `QueryCommentsAsync()` | NEW in V3 |
| Polls | `client.CastPollVoteAsync()` / `DeletePollVoteAsync()` | NEW in V3 |
| `batchOps.FollowManyAsync()` | `client.FollowBatchAsync(request)` | Batch follow |
| `batchOps.UnfollowManyAsync()` | `client.UnfollowBatchAsync(request)` | Batch unfollow |
| `feed.FollowStatsAsync()` | Not available | Custom implementation needed |

## Key Differences Summary

1. **Response Wrapping**: All V3 responses are wrapped in `StreamResponse<T>`
2. **Pagination**: V2 uses `offset`, V3 uses token-based (`next`/`prev`)
3. **Feed Context**: V2 implicit, V3 explicit in requests
4. **Activity Model**: V2 uses Actor/Verb/Object, V3 uses UserID/Type/Text
5. **Object Field**: V2 has `Object` (string reference), V3 removed - use `filter_tags`/`search_data` for filtering/searching
6. **Custom Data**: V2 can store in Object field, V3 use `custom` field (not in Object)
7. **Reactions**: V2 separate service, V3 tied to activities/comments with different API
8. **Comments**: V2 not available, V3 first-class feature
9. **Polls**: V2 not available, V3 available
10. **Query Pattern**: V3 uses request objects instead of method parameters
11. **To-Targets**: V2 incremental, V3 replace-all (must combine manually)
