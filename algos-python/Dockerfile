FROM python:3.12.4-slim
ENV PYTHONUNBUFFERED=1
WORKDIR /app
RUN pip install debugpy
COPY . .
RUN pip install --no-cache-dir -r requirements.txt
CMD ["python", "-m", "debugpy", "--listen", "0.0.0.0:5678", "main.py"]
