FROM python:3.11-slim

WORKDIR /app

# Install OS deps for NLTK, then Python deps
RUN apt-get update && apt-get install -y --no-install-recommends \
    gcc build-essential \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt

# Pre-download NLTK data used by seed_builder
RUN python - <<'PY'
import nltk
for pkg in ["vader_lexicon", "punkt", "averaged_perceptron_tagger", "wordnet", "stopwords"]:
    nltk.download(pkg, quiet=True)
PY

COPY . /app

# Default command runs seed_builder on /app/data
CMD ["python", "seed_builder.py", "--data-dir", "data", "--reviews-out", "/dev/null", "--seeds-out", "seeds.csv"]
