# Development Dockerfile for React Client (Vite)
FROM node:20-alpine AS development

WORKDIR /app

# Install dependencies first for better caching
COPY package*.json ./
RUN npm install

# Copy source code
COPY . ./

# Expose Vite dev server port
EXPOSE 5173

# Set environment variables
ENV NODE_ENV=development
ENV VITE_API_URL=http://api:5000

# Start Vite dev server with host binding for container access
CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0", "--port", "5173"]