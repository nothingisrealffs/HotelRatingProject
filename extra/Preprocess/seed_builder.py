import argparse
import csv
import re
import sys
from pathlib import Path

import nltk
from collections import defaultdict, Counter

from nltk import pos_tag, word_tokenize
from nltk.corpus import wordnet, stopwords
from nltk.sentiment import SentimentIntensityAnalyzer
from nltk.stem import WordNetLemmatizer
from nltk.tokenize import sent_tokenize

ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))
from apmain import read_document

nltk.download("vader_lexicon", quiet=True)
nltk.download("punkt", quiet=True)
nltk.download("averaged_perceptron_tagger", quiet=True)
nltk.download("wordnet", quiet=True)
nltk.download("stopwords", quiet=True)

TOKEN_RE = re.compile(r"^[a-z]+$")
JUNK_TOKENS = {"quot"}
FILLER_STOPWORDS = {"just", "even", "still", "get", "got"}

ASPECT_KEYWORDS = {
    "cleanliness": [
        "clean",
        "dirty",
        "filthy",
        "spotless",
        "tidy",
        "stain",
        "grimy",
        "dusty",
        "fresh",
    ],
    "service": [
        "service",
        "staff",
        "reception",
        "manager",
        "rude",
        "helpful",
        "check",
        "checkin",
        "checkout",
        "housekeeping",
        "front desk",
    ],
    "location": [
        "location",
        "walk",
        "near",
        "close",
        "distance",
        "station",
        "tube",
        "area",
        "convenient",
        "central",
        "neighborhood",
    ],
    "comfort": [
        "bed",
        "room",
        "comfortable",
        "noise",
        "noisy",
        "quiet",
        "hot",
        "cold",
        "air",
        "ac",
        "heating",
        "pillow",
        "mattress",
        "sleep",
    ],
    "price": [
        "price",
        "cost",
        "expensive",
        "cheap",
        "value",
        "worth",
        "deal",
        "rate",
        "charge",
        "fee",
    ],
}


def parse_reviews(raw_text: str):
    parsed = []
    for raw_line in raw_text.splitlines():
        if not raw_line.strip():
            continue

        parts = raw_line.split("\t")
        if len(parts) < 3:
            continue

        date = parts[0].strip()
        subject = parts[1].strip()
        review = parts[2].strip()

        if subject or review:
            parsed.append((date, subject, review))
    return parsed


def derive_hotel_name(path: Path) -> str:
    stem = path.stem.replace("_", " ").replace("-", " ")
    return stem.title()


def derive_city(path: Path, data_dir: Path) -> str:
    try:
        rel = path.relative_to(data_dir)
        city_part = rel.parts[0] if len(rel.parts) > 1 else ""
    except Exception:
        city_part = ""
    return city_part.replace("_", " ").replace("-", " ").title()


def iter_hotel_files(data_dir):
    data_dir = Path(data_dir)
    skip_exts = {".rar", ".zip", ".gz"}
    for path in sorted(data_dir.iterdir()):
        if path.is_file():
            if path.suffix.lower() in skip_exts:
                continue
            yield path
        elif path.is_dir():
            for child in sorted(path.iterdir()):
                if child.is_file() and child.suffix.lower() not in skip_exts:
                    yield child


def label_sentiment(compound_score: float, pos_threshold: float = 0.05, neg_threshold: float = -0.05) -> str:
    """Turn a VADER compound score into a coarse label."""
    if compound_score >= pos_threshold:
        return "positive"
    if compound_score <= neg_threshold:
        return "negative"
    return "neutral"


def get_wordnet_pos(treebank_tag: str):
    """Convert Treebank POS tags to WordNet POS tags."""
    if treebank_tag.startswith("J"):
        return wordnet.ADJ
    if treebank_tag.startswith("V"):
        return wordnet.VERB
    if treebank_tag.startswith("N"):
        return wordnet.NOUN
    if treebank_tag.startswith("R"):
        return wordnet.ADV
    return wordnet.NOUN


def lemmatize_tokens(text: str):
    """Lemmatize tokens for broader keyword matching."""
    lemmatizer = WordNetLemmatizer()
    tokens = word_tokenize(text.lower())
    pos_tags = pos_tag(tokens)
    return [lemmatizer.lemmatize(tok, pos=get_wordnet_pos(pos)) for tok, pos in pos_tags]


def build_keyword_sets():
    """Pre-lemmatize aspect keywords for faster matching and keep original seed words."""
    keyword_sets = {}
    for aspect, words in ASPECT_KEYWORDS.items():
        lemma_map = {}
        for w in words:
            for lemma in lemmatize_tokens(w):
                lemma_map[lemma] = w
        keyword_sets[aspect] = lemma_map
    return keyword_sets


def score_aspects(review_text: str, sia: SentimentIntensityAnalyzer, keyword_sets):
    aspect_scores = {aspect: [] for aspect in ASPECT_KEYWORDS}
    for sentence in sent_tokenize(review_text):
        lemmas = set(lemmatize_tokens(sentence))
        matched_aspects = []
        for aspect, lemma_map in keyword_sets.items():
            overlap = lemmas & set(lemma_map.keys())
            if overlap:
                matched_aspects.append((aspect, overlap))
        if not matched_aspects:
            continue
        compound = sia.polarity_scores(sentence)["compound"]
        for aspect, overlap in matched_aspects:
            aspect_scores[aspect].append(compound)

    summarized = {}
    for aspect, values in aspect_scores.items():
        if not values:
            summarized[aspect] = (None, "neutral")
        else:
            avg = sum(values) / len(values)
            summarized[aspect] = (avg, label_sentiment(avg))
    return summarized


def build_seed_csv(data_dir="testdata", output_path="review_seeds.csv"):
    sia = SentimentIntensityAnalyzer()
    keyword_sets = build_keyword_sets()
    records = []

    data_dir_path = Path(data_dir)
    hotel_paths = list(iter_hotel_files(data_dir_path))
    print(f"[reviews] Found {len(hotel_paths)} files under {data_dir_path}")
    for idx, hotel_path in enumerate(hotel_paths, 1):
        print(f"[reviews] ({idx}/{len(hotel_paths)}) {hotel_path}", flush=True)
        text = read_document(hotel_path)
        hotel_name = derive_hotel_name(hotel_path)
        city_name = derive_city(hotel_path, data_dir_path)
        reviews = parse_reviews(text)

        for date, subject, review in reviews:
            overall_compound = sia.polarity_scores(review)["compound"]
            aspect_results = score_aspects(review, sia, keyword_sets)
            record = {
                "hotel": hotel_name,
                "city": city_name,
                "date": date,
                "subject": subject,
                "review": review,
                "sentiment": label_sentiment(overall_compound),
                "compound": f"{overall_compound:.4f}",
            }
            for aspect, (score, label) in aspect_results.items():
                record[f"{aspect}_score"] = "" if score is None else f"{score:.4f}"
                record[f"{aspect}_label"] = label if score is not None else ""

            records.append(record)

    fieldnames = [
        "hotel",
        "city",
        "date",
        "subject",
        "review",
        "sentiment",
        "compound",
    ]
    for aspect in ASPECT_KEYWORDS:
        fieldnames.extend([f"{aspect}_score", f"{aspect}_label"])

    output_path = Path(output_path)
    with output_path.open("w", newline="", encoding="utf-8") as csvfile:
        writer = csv.DictWriter(csvfile, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(records)

    print(f"Wrote {len(records)} rows to {output_path} from {data_dir}")


def build_aspect_seed_terms(
    data_dir="testdata",
    output_path="aspect_seed_terms.csv",
    by_hotel_output_path="aspect_seed_terms_by_hotel.csv",
    min_abs_weight=1e-6,
    min_phrase_freq=2,
):

    sia = SentimentIntensityAnalyzer()
    keyword_sets = build_keyword_sets()

    seed_rows = []
    seed_rows_by_hotel = []
    phrase_support = Counter()  # count across hotels
    stop_words = set(stopwords.words("english"))
    boundary_stopwords = stop_words | {"and", "but", "or"}

    data_dir_path = Path(data_dir)
    hotel_paths = list(iter_hotel_files(data_dir_path))
    print(f"[phrases] Found {len(hotel_paths)} files under {data_dir_path}")

    for idx, hotel_path in enumerate(hotel_paths, 1):
        print(f"[phrases] ({idx}/{len(hotel_paths)}) {hotel_path}", flush=True)
        text = read_document(hotel_path)
        reviews = parse_reviews(text)
        hotel_name = derive_hotel_name(hotel_path)
        city_name = derive_city(hotel_path, data_dir_path)

        phrase_scores = defaultdict(list)  

        for _, _, review in reviews:
            for sentence in sent_tokenize(review):
                raw_tokens = word_tokenize(sentence)
                tokens = []
                for t in raw_tokens:
                    tl = t.lower()
                    if not TOKEN_RE.match(tl):
                        continue
                    if tl in stop_words or tl in JUNK_TOKENS or tl in FILLER_STOPWORDS:
                        continue
                    tokens.append(tl)
                if not tokens:
                    continue

                pos_tags = pos_tag(tokens)
                lemmas = lemmatize_tokens(" ".join(tokens))
                for aspect, lemma_map in keyword_sets.items():
                    compound = sia.polarity_scores(sentence)["compound"]
                    indices = [i for i, lem in enumerate(lemmas) if lem in lemma_map]
                    if not indices:
                        continue
                    for idx in indices:
                        for phrase in generate_phrases(
                            tokens,
                            idx,
                            pos_tags=pos_tags,
                            boundary_stopwords=boundary_stopwords,
                            min_len=2,
                            max_len=3,
                        ):
                            phrase_scores[(aspect, phrase)].append(compound)

        for (aspect, phrase), scores in phrase_scores.items():
            support = len(scores)
            avg = sum(scores) / support
            if support < min_phrase_freq or abs(avg) <= min_abs_weight:
                continue  
            seed_rows_by_hotel.append(
                {
                    "hotel": hotel_name,
                    "city": city_name,
                    "feature_name": aspect.title(),
                    "seed_phrase": phrase,
                    "weight": f"{avg:.4f}",
                }
            )
            seed_rows.append(
                {
                    "feature_name": aspect.title(),
                    "seed_phrase": phrase,
                    "weight": f"{avg:.4f}",
                }
            )
            phrase_support[(aspect, phrase)] += support

    agg_weights = defaultdict(float)
    agg_support = defaultdict(int)
    for row in seed_rows:
        key = (row["feature_name"], row["seed_phrase"])
        w = float(row["weight"])
        agg_weights[key] += w * phrase_support.get(key, 1)
        agg_support[key] += phrase_support.get(key, 1)

    deduped = []
    for (feature, phrase), total_w in agg_weights.items():
        supp = agg_support[(feature, phrase)]
        if supp < min_phrase_freq:
            continue
        avg_w = total_w / supp
        if abs(avg_w) <= min_abs_weight:
            continue
        deduped.append(
            {
                "feature_name": feature.title(),
                "seed_phrase": phrase,
                "weight": f"{avg_w:.4f}",
            }
        )

    Path(output_path).open("w", newline="", encoding="utf-8").close()
    with Path(output_path).open("w", newline="", encoding="utf-8") as csvfile:
        fieldnames = ["feature_name", "seed_phrase", "weight"]
        writer = csv.DictWriter(csvfile, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(deduped)

    if by_hotel_output_path:
        with Path(by_hotel_output_path).open("w", newline="", encoding="utf-8") as csvfile:
            fieldnames = ["hotel", "city", "feature_name", "seed_phrase", "weight"]
            writer = csv.DictWriter(csvfile, fieldnames=fieldnames, extrasaction="ignore")
            writer.writeheader()
            writer.writerows(seed_rows_by_hotel)

    msg = f"Wrote {len(deduped)} aspect seed rows to {output_path}"
    if by_hotel_output_path:
        msg += f" and {len(seed_rows_by_hotel)} rows to {by_hotel_output_path}"
    msg += f" from {data_dir}"
    print(msg)


def generate_phrases(tokens, idx, pos_tags=None, max_len=3, min_len=2, boundary_stopwords=None):
    n = len(tokens)
    keyword = tokens[idx]
    phrases = set()
    boundary_stopwords = boundary_stopwords or set()

    prev_tokens = tokens[max(0, idx - 2):idx]
    next_tokens = tokens[idx + 1:min(n, idx + 3)]

    for left_len in range(0, min(2, len(prev_tokens)) + 1):
        for right_len in range(0, min(2, len(next_tokens)) + 1):
            phrase_tokens = prev_tokens[len(prev_tokens) - left_len:] + [keyword] + next_tokens[:right_len]
            if min_len <= len(phrase_tokens) <= max_len:
                while phrase_tokens and phrase_tokens[0] in boundary_stopwords:
                    phrase_tokens = phrase_tokens[1:]
                while phrase_tokens and phrase_tokens[-1] in boundary_stopwords:
                    phrase_tokens = phrase_tokens[:-1]
                if phrase_tokens:
                    if pos_tags:
                        kw_pos = pos_tags[idx][1] if idx < len(pos_tags) else ""
                        left_pos = pos_tags[idx - 1][1] if idx - 1 >= 0 else ""
                        if not kw_pos.startswith("N"):
                            continue
                        if phrase_tokens[0] == keyword:
                            pass
                        else:
                            if not (left_pos.startswith("J") or left_pos.startswith("N")):
                                continue
                    phrase = " ".join(phrase_tokens)
                    phrases.add(phrase)

    return phrases


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Build seeds and aspect phrases from hotel review files.")
    parser.add_argument("--data-dir", default="data", help="Root directory containing hotel review files.")
    parser.add_argument("--reviews-out", default="review_seeds.csv", help="Path for aggregated review seeds CSV.")
    parser.add_argument("--seeds-out", default="seeds.csv", help="Path for deduped seeds CSV (3 columns).")
    parser.add_argument(
        "--seeds-by-hotel-out",
        default="",
        help="Optional path for seeds with hotel and city columns. Omit or leave blank to skip.",
    )
    args = parser.parse_args()

    build_seed_csv(data_dir=args.data_dir, output_path=args.reviews_out)
    build_aspect_seed_terms(
        data_dir=args.data_dir,
        output_path=args.seeds_out,
        by_hotel_output_path=args.seeds_by_hotel_out if args.seeds_by_hotel_out else None,
    )
