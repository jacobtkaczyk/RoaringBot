# Stage 1: Build the application assets
FROM node:20-alpine AS build

# Set the working directory
WORKDIR /app

# Copy package.json and package-lock.json and install dependencies
COPY package*.json ./
RUN npm install

# Copy the rest of the application source code
COPY . .

# Build the application for production
RUN npm run build


# Stage 2: Serve the application using Nginx
FROM nginx:stable-alpine

# Copy the built assets from the 'build' stage
# The build output directory might be 'dist' or 'build' depending on your framework.
# Adjust '/app/build' if your output folder is different (e.g., '/app/dist').
COPY --from=build /app/dist /usr/share/nginx/html

# Expose port 80 to the Docker host
EXPOSE 80

# Start Nginx when the container launches
CMD ["nginx", "-g", "daemon off;"]