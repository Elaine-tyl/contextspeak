import 'reflect-metadata';
import { Controller, Get, Module } from '@nestjs/common';
import { JwtGuard } from './auth/jwt.guard';
import type { AuthConfig } from './auth/identity';
import { NestFactory } from '@nestjs/core';
@Controller('v1/lookups')
class Boundary { @Get() list() { return { items: [] }; } }
@Module({ controllers: [Boundary] }) class ApiModule {}
export async function createApp(config: AuthConfig) { const app = await NestFactory.create(ApiModule, { logger: false }); app.useGlobalGuards(new JwtGuard(config)); await app.init(); return app; }
