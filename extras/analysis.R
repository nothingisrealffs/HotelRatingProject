Sys.setenv("R_TEXTDATA_DOWNLOAD"="TRUE")
# analysis.R
# Robust, non-interactive hotel review analysis pipeline
# - Chunked UDPIPE annotation for performance
# - Distinct-review phrase counts
# - Balanced dynamic seed selection
# - Writes DB-ready CSVs to /output by default


suppressPackageStartupMessages({
  library(tidyverse)
  library(tidytext)
  library(stringr)
  library(stringi)
  library(lubridate)
  library(udpipe)
  library(reshape2)
  # data.table used for faster grouping if available
  if (requireNamespace("data.table", quietly = TRUE)) library(data.table)
})

# -----------------------
# Configurable constants
# -----------------------
DATA_DIR <- Sys.getenv("DATA_DIR", "/Data")
OUTPUT_DIR <- Sys.getenv("OUTPUT_DIR", "/output")
UDPIPE_FILE <- Sys.getenv("UDPIPE_FILE", "english-ewt-ud-2.5-191206.udpipe")

MAX_SEEDS_PER_SENTIMENT <- as.integer(Sys.getenv("MAX_SEEDS_PER_SENTIMENT", 12))
POS_NEG_BALANCE_RATIO <- as.numeric(Sys.getenv("POS_NEG_BALANCE_RATIO", 1.2))
MIN_DISTINCT_REVIEW_FREQ <- as.integer(Sys.getenv("MIN_DISTINCT_REVIEW_FREQ", 5))
UDPIPE_CHUNK_SIZE <- as.integer(Sys.getenv("UDPIPE_CHUNK_SIZE", 2000))  # number of docs per udpipe annotate call
C_INTENSITY_FACTOR <- as.numeric(Sys.getenv("C_INTENSITY_FACTOR", 0.8))

# blacklist / stop-like tokens (expandable)
bad_terms <- c("quot", "amp", "nbsp", "lt", "gt", "x20", "rsquo", "ldquo", "rdquo", "apos",
               "http", "https", "www", "hotel", "room", "stay", "place", "nice", "great", "good")

# -----------------------
# Utility: ensure output exists
# -----------------------
if (!dir.exists(OUTPUT_DIR)) dir.create(OUTPUT_DIR, recursive = TRUE, showWarnings = FALSE)

# -----------------------
# Text cleaning
# -----------------------
clean_text <- function(text) {
  text %>%
    stri_enc_toutf8(is_unknown_8bit = TRUE, validate = FALSE) %>%
    iconv(from = "UTF-8", to = "UTF-8", sub = "") %>%
    str_replace_all("&[a-zA-Z]+;", " ") %>%
    str_replace_all("&#[0-9]+;?", " ") %>%
    str_replace_all("\\\\x[0-9A-Fa-f]{2}", " ") %>%  # FIXED: four backslashes
    str_replace_all("https?://[^\\s]+", " ") %>%
    str_replace_all("[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}", " ") %>%
    str_replace_all("[[:cntrl:]]", " ") %>%
    str_replace_all("[^\\p{L}\\s]", " ") %>%
    str_squish() %>%
    str_to_lower()
}

# -----------------------
# File parsing (kept simple & robust)
# -----------------------
parse_filename <- function(filename) {
  parts <- str_split(filename, "_")[[1]]
  if (length(parts) >= 3) {
    country <- parts[1]
    city <- parts[2]
    hotel_parts <- parts[3:length(parts)]
    hotel_name <- str_to_title(str_replace_all(paste(hotel_parts, collapse = " "), "_", " "))
    return(list(country = str_to_upper(country), city = str_to_title(city), hotel_name = hotel_name))
  }
  list(country = "Unknown", city = "Unknown", hotel_name = filename)
}

read_review_file <- function(filepath) {
  tryCatch({
    content <- tryCatch(readLines(filepath, encoding = "CP1251", warn = FALSE),
                        error = function(e) tryCatch(readLines(filepath, encoding = "UTF-8", warn = FALSE),
                                                     error = function(e2) readLines(filepath, encoding = "latin1", warn = FALSE)))
    content <- stri_enc_toutf8(content, is_unknown_8bit = TRUE, validate = FALSE)
    content <- iconv(content, from = "UTF-8", to = "UTF-8", sub = "")
    content <- content[!is.na(content) & nchar(content) > 0]
    paste(content, collapse = "\n")
  }, error = function(e) {
    warning("Error reading file: ", basename(filepath), " -> ", e$message)
    NA_character_
  })
}

process_single_hotel_file <- function(filepath, city_name) {
  filename <- basename(filepath)
  info <- parse_filename(filename)
  ext <- tolower(tools::file_ext(filename))

  if (ext == "csv") {
    df_csv <- tryCatch(readr::read_csv(filepath, show_col_types = FALSE), error = function(e) NULL)
    if (!is.null(df_csv)) {
      # heuristics for text column
      possible_text_cols <- c("review", "review_text", "text", "body", "comments")
      name_lc <- tolower(names(df_csv))
      text_col_idx <- which(name_lc %in% possible_text_cols)
      text_col <- if (length(text_col_idx) > 0) names(df_csv)[text_col_idx[1]] else NULL

      if (is.null(text_col)) {
        char_cols <- names(df_csv)[sapply(df_csv, function(x) is.character(x) || is.factor(x))]
        if (length(char_cols) > 0) text_col <- char_cols[1]
      }

      if (!is.null(text_col)) {
        reviews_df <- tibble(
          review_text = as.character(df_csv[[text_col]])
        ) %>% filter(!is.na(review_text) & nchar(str_trim(review_text)) > 10)

        if (nrow(reviews_df) == 0) return(tibble())

        return(reviews_df %>%
                 mutate(country = info$country, city = city_name, hotel_name = info$hotel_name,
                        review_text = clean_text(review_text),
                        review_date = NA_Date_, reviewer_name = "Anonymous") %>%
                 select(hotel_name, review_text, reviewer_name, review_date))
      }
    }
  }

  content <- read_review_file(filepath)
  if (is.na(content) || nchar(content) == 0) return(tibble())
  lines <- str_split(content, "\n")[[1]]
  lines <- lines[nchar(str_trim(lines)) > 0]

  reviews_list <- list()
  for (line in lines) {
    parts <- str_split(line, "\t")[[1]]
    if (length(parts) >= 3 && nchar(str_trim(parts[3])) > 10) {
      reviews_list[[length(reviews_list) + 1]] <- tibble(review_text = str_trim(parts[3]))
    } else if (nchar(str_trim(line)) > 30) {
      reviews_list[[length(reviews_list) + 1]] <- tibble(review_text = str_trim(line))
    }
  }
  if (length(reviews_list) == 0) return(tibble())

  bind_rows(reviews_list) %>%
    mutate(country = info$country, city = city_name, hotel_name = info$hotel_name,
           review_text = clean_text(review_text),
           review_date = NA_Date_, reviewer_name = "Anonymous") %>%
    select(hotel_name, review_text, reviewer_name, review_date)
}

process_all_hotels <- function(base_dir = DATA_DIR) {
  if (!dir.exists(base_dir)) stop("Base data directory does not exist: ", base_dir)
  all_reviews <- vector("list")
  city_folders <- list.dirs(base_dir, full.names = TRUE, recursive = FALSE)

  for (city_folder in city_folders) {
    city_name <- basename(city_folder)
    hotel_files <- list.files(city_folder, full.names = TRUE, recursive = FALSE)
    for (hotel_file in hotel_files) {
      if (startsWith(basename(hotel_file), ".")) next
      hotel_reviews <- tryCatch(process_single_hotel_file(hotel_file, city_name),
                                error = function(e) { warning(e); return(tibble()) })
      if (nrow(hotel_reviews) > 0) all_reviews[[length(all_reviews)+1]] <- hotel_reviews
    }
  }
  if (length(all_reviews) == 0) return(tibble())
  reviews_df <- bind_rows(all_reviews) %>% mutate(review_id = row_number())
  # remove exact duplicates early (saves cost downstream)
  reviews_df <- distinct(reviews_df, review_text, .keep_all = TRUE) %>% mutate(review_id = row_number())
  reviews_df
}

# -----------------------
# Lexicons
# -----------------------
load_lexicons <- function() {
  # ensure textdata will attempt non-interactive download if needed
  Sys.setenv("R_TEXTDATA_DOWNLOAD" = "TRUE", "R_TEXTDATA_DELETE" = "FALSE")
  options(textdata.download = TRUE)

  bing <- get_sentiments("bing")

  afinn <- tryCatch({
    textdata::lexicon_afinn(manual_download = FALSE, delete = FALSE, return_path = FALSE, clean = TRUE)
  }, error = function(e) {
    message("textdata::lexicon_afinn() failed: ", e$message)
    message("Falling back to tidytext::get_sentiments('afinn') (may prompt if not already downloaded).")
    # fallback attempt (wrapped) — if this still prompts, the earlier explicit call is preferred
    tryCatch(get_sentiments("afinn"), error = function(e2) tibble())
  })

  list(bing = bing, afinn = afinn)
}



ensure_udpipe <- function(file = UDPIPE_FILE) {
  if (!file.exists(file)) {
    message("UDPIPE model not found at ", file, " — attempting download to working dir")
    udpipe_download_model(language = "english-ewt", model_dir = getwd())
    candidates <- list.files(getwd(), pattern = "\\.udpipe$", full.names = TRUE)
    if (length(candidates) > 0) file <- candidates[1]
  }
  if (!file.exists(file)) stop("UDPIPE model not found and download failed. Set UDPIPE_FILE to mounted model.")
  udpipe_load_model(file = file)
}

generate_lemma_ngrams_chunked <- function(reviews_df, ud_model, chunk_size = UDPIPE_CHUNK_SIZE) {
  n <- nrow(reviews_df)
  if (n == 0) return(tibble())
  ranges <- split(1:n, ceiling(seq_along(1:n)/chunk_size))
  all_phrases <- vector("list")

  for (chunk_idx in seq_along(ranges)) {
    idxs <- ranges[[chunk_idx]]
    doc_texts <- reviews_df$review_text[idxs]
    doc_ids <- reviews_df$review_id[idxs]

    ann <- udpipe_annotate(ud_model, x = doc_texts, doc_id = doc_ids) %>% as_tibble()
    if (nrow(ann) == 0) next
    ann <- ann %>% mutate(token_id = as.integer(token_id)) %>% arrange(doc_id, paragraph_id, sentence_id, token_id)

    lemma_ngrams <- ann %>%
      group_by(doc_id) %>%
      mutate(next_lemma = lead(lemma), next2_lemma = lead(lemma, 2),
             next_token_id = lead(token_id), next2_token_id = lead(token_id, 2)) %>%
      ungroup() %>%
      transmute(doc_id,
                bigram = if_else(!is.na(next_lemma) & token_id + 1 == next_token_id,
                                 paste(lemma, next_lemma, sep = " "), NA_character_),
                trigram = if_else(!is.na(next2_lemma) & token_id + 2 == next2_token_id,
                                  paste(lemma, next_lemma, next2_lemma, sep = " "), NA_character_))

    bigrams <- lemma_ngrams %>% select(doc_id, bigram) %>% filter(!is.na(bigram)) %>% distinct(doc_id, bigram)
    trigrams <- lemma_ngrams %>% select(doc_id, trigram) %>% filter(!is.na(trigram)) %>% distinct(doc_id, trigram)

    if (nrow(bigrams) > 0) {
      bcount <- bigrams %>% count(bigram, name = "distinct_review_count") %>% rename(ngram = bigram)
      all_phrases[[length(all_phrases) + 1]] <- bcount
    }
    if (nrow(trigrams) > 0) {
      tcount <- trigrams %>% count(trigram, name = "distinct_review_count") %>% rename(ngram = trigram)
      all_phrases[[length(all_phrases) + 1]] <- tcount
    }
    message("Annotated chunk ", chunk_idx, " / ", length(ranges), "  (docs: ", length(idxs), ")")
  }

  if (length(all_phrases) == 0) return(tibble())
  result <- bind_rows(all_phrases) %>%
    group_by(ngram) %>%
    summarise(distinct_review_count = sum(distinct_review_count), .groups = "drop") %>%
    arrange(desc(distinct_review_count))
  result
}

# -----------------------
# Improved sentiment classification for phrases
# -----------------------
classify_phrase_sentiment_improved <- function(phrases_df, lexicons) {
  if (nrow(phrases_df) == 0) return(tibble())
  res <- phrases_df %>%
    rowwise() %>%
    mutate(words = list(str_split(ngram, " ")[[1]]),
           pos_count = sum(words %in% filter(lexicons$bing, sentiment == "positive")$word),
           neg_count = sum(words %in% filter(lexicons$bing, sentiment == "negative")$word),
           afinn_scores = list(lexicons$afinn$value[match(words, lexicons$afinn$word)]),
           afinn_score = sum(afinn_scores[[1]], na.rm = TRUE)) %>%
    ungroup() %>%
    mutate(
      sentiment = case_when(
        afinn_score > 0 ~ "positive",
        afinn_score < 0 ~ "negative",
        pos_count > neg_count & (pos_count - neg_count) >= 1 ~ "positive",
        neg_count > pos_count & (neg_count - pos_count) >= 1 ~ "negative",
        TRUE ~ "neutral"
      ),
      weight = case_when(
        afinn_score != 0 ~ afinn_score,
        sentiment == "positive" ~ 1,
        sentiment == "negative" ~ -1,
        TRUE ~ 0
      )
    ) %>%
    select(ngram, distinct_review_count, sentiment, weight)
  res
}

# -----------------------
# Balanced seed selection
# -----------------------
select_balanced_seeds <- function(candidates, max_per_sentiment = MAX_SEEDS_PER_SENTIMENT,
                                  balance_ratio = POS_NEG_BALANCE_RATIO,
                                  min_reviews = MIN_DISTINCT_REVIEW_FREQ) {
  candidates <- candidates %>% filter(distinct_review_count >= min_reviews)
  if (nrow(candidates) == 0) return(tibble())
  pos_cand <- candidates %>% filter(sentiment == "positive") %>% arrange(desc(distinct_review_count))
  neg_cand <- candidates %>% filter(sentiment == "negative") %>% arrange(desc(distinct_review_count))

  n_neg <- nrow(neg_cand)
  if (n_neg > 0) {
    pos_limit <- min(max_per_sentiment, ceiling(balance_ratio * n_neg))
    pos_sel <- head(pos_cand, pos_limit)
    neg_sel <- head(neg_cand, max_per_sentiment)
  } else {
    pos_sel <- head(pos_cand, max_per_sentiment)
    neg_sel <- head(neg_cand, max_per_sentiment)
  }
  bind_rows(pos_sel, neg_sel) %>%
    arrange(desc(abs(weight)), desc(distinct_review_count))
}

# -----------------------
# Main pipeline
# -----------------------
main_fixed <- function(base_dir = DATA_DIR, output_dir = OUTPUT_DIR, udpipe_model_file = UDPIPE_FILE) {
  message("=== Hotel Review Analysis (non-interactive) ===")
  message("Data dir: ", base_dir, "  Output dir: ", output_dir)

  reviews <- process_all_hotels(base_dir)
  if (nrow(reviews) == 0) {
    message("No reviews found under ", base_dir, ". Nothing to do.")
    return(list(reviews = tibble(), seed_words = tibble()))
  }
  message("Loaded ", format(nrow(reviews), big.mark=","), " reviews (deduped).")

  lexicons <- load_lexicons()
  ud_model <- ensure_udpipe(udpipe_model_file)

  # build lemma-based ngrams with chunked udpipe annotation
  message("Generating lemma-based ngrams (chunked UDPIPE)...")
  phrase_table <- generate_lemma_ngrams_chunked(reviews, ud_model, chunk_size = UDPIPE_CHUNK_SIZE)

  # filter out bad terms & short phrases
  phrase_table <- phrase_table %>%
    filter(!str_detect(ngram, paste0("\\b(", paste(bad_terms, collapse = "|"), ")\\b")),
           str_detect(ngram, "^[a-z ]{4,}$"))

  message("Candidate phrases: ", nrow(phrase_table))

  # feature map (same categories as original)
  feature_map <- tribble(
    ~feature, ~search_terms,
    "cleanliness", c("clean", "spotless", "dirty", "filthy", "tidy"),
    "service",     c("friendly", "helpful", "rude", "attentive", "unhelpful", "polite"),
    "location",    c("central", "convenient", "close", "walkable", "far", "remote"),
    "comfort",     c("comfortable", "uncomfortable", "cozy", "cramped", "spacious"),
    "price",       c("expensive", "cheap", "affordable", "overpriced", "reasonable", "costly")
  )

  all_seed_words <- list()
  total_reviews <- nrow(reviews)

  for (i in seq_len(nrow(feature_map))) {
    feat <- feature_map$feature[i]
    search_terms <- feature_map$search_terms[[i]]
    message("Discovering seeds for feature: ", feat)

    pattern <- paste0("\\b(", paste(search_terms, collapse = "|"), ")\\b")
    cand_phrases <- phrase_table %>% filter(str_detect(ngram, regex(pattern, ignore_case = TRUE))) %>%
      mutate(feature = feat)

    if (nrow(cand_phrases) == 0) {
      cand_phrases <- phrase_table %>% head(100) %>% mutate(feature = feat)
    }

    classified <- classify_phrase_sentiment_improved(cand_phrases, lexicons) %>%
      mutate(feature = feat)

    seeds_selected <- select_balanced_seeds(classified,
                                           max_per_sentiment = MAX_SEEDS_PER_SENTIMENT,
                                           balance_ratio = POS_NEG_BALANCE_RATIO,
                                           min_reviews = MIN_DISTINCT_REVIEW_FREQ)

    if (nrow(seeds_selected) > 0) {
      seeds_selected <- seeds_selected %>%
        mutate(frequency = distinct_review_count,
               freq_reviews_pct = 100 * distinct_review_count / total_reviews) %>%
        select(feature, ngram, sentiment, weight, frequency, distinct_review_count, freq_reviews_pct)
      all_seed_words[[feat]] <- seeds_selected
      message("  -> Selected ", nrow(seeds_selected), " seeds for ", feat)
    } else {
      message("  -> No seeds selected for ", feat, " (try lowering MIN_DISTINCT_REVIEW_FREQ)")
    }
  }

  seed_words <- bind_rows(all_seed_words)

  # Write output CSVs (DB-compatible)
  features_df <- tibble(feature_name = str_to_title(unique(seed_words$feature)),
                        description = "Feature category discovered from review data", is_active = "Y")
  write_csv(features_df, file.path(output_dir, "features.csv"))

  seeds_df <- seed_words %>%
    mutate(feature_name = str_to_title(feature), seed_phrase = ngram) %>%
    select(feature_name, seed_phrase, weight, frequency, distinct_review_count, freq_reviews_pct) %>%
    arrange(feature_name, desc(distinct_review_count))
  write_csv(seeds_df, file.path(output_dir, "seeds.csv"))

  hotels_df <- reviews %>%
    distinct(hotel_name, city, country) %>%
    arrange(city, hotel_name) %>%
    mutate(address = paste(hotel_name, city, sep = ", "), star_rating = NA_real_) %>%
    select(hotel_name, address, city, country, star_rating)
  write_csv(hotels_df, file.path(output_dir, "hotels.csv"))

  reviews_out <- reviews %>%
    select(hotel_name, review_text, reviewer_name, review_date) %>%
    mutate(overall_rating = NA_real_)
  write_csv(reviews_out, file.path(output_dir, "reviews.csv"))

  # diagnostics
  write_csv(seed_words %>% arrange(feature, desc(distinct_review_count)),
            file.path(output_dir, "seed_diagnostics.csv"))

  # quick summary print
  message("=== Pipeline complete ===")
  message("Wrote files to: ", normalizePath(output_dir))
  message("Features: ", nrow(features_df), "  Seeds: ", nrow(seeds_df), "  Hotels: ", nrow(hotels_df))
  invisible(list(reviews = reviews, seed_words = seed_words, seeds_df = seeds_df, features_df = features_df, hotels_df = hotels_df))
}

# -----------------------
# Run the pipeline non-interactively exactly like you requested
# -----------------------

results <- main_fixed(base_dir = "/content/drive/MyDrive/data", output_dir = "/content/drive/MyDrive/output")

# print a tiny manifest for convenience
if (!is.null(results$seeds_df)) {
  cat("\nWROTE:", file.path(OUTPUT_DIR, "features.csv"), "\n")
  cat("       ", file.path(OUTPUT_DIR, "seeds.csv"), "\n")
  cat("       ", file.path(OUTPUT_DIR, "hotels.csv"), "\n")
  cat("       ", file.path(OUTPUT_DIR, "reviews.csv"), "\n")
  cat("Diagnostics:", file.path(OUTPUT_DIR, "seed_diagnostics.csv"), "\n")
} else {
  cat("\nNo output produced (no reviews found or early exit).\n")
}
