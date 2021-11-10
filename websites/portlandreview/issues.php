<?php
/**
 *
 Template Name: Issues
 *
 * The template for displaying Archive pages.
 *
 * Used to display archive-type pages if nothing more specific matches a query.
 * For example, puts together date-based pages if no date.php file exists.
 *
 * If you'd like to further customize these archive views, you may create a
 * new template file for each specific one. For example, Twenty Thirteen
 * already has tag.php for Tag archives, category.php for Category archives,
 * and author.php for Author archives.
 *
 * Learn more: http://codex.wordpress.org/Template_Hierarchy
 *
 * @package WordPress
 * @subpackage Twenty_Thirteen
 * @since Twenty Thirteen 1.0
 */

get_header(); ?>

	<div id="issue-holder">
		<?php /* The loop */ ?>
			<?php while ( have_posts() ) : the_post(); ?>
					<header class="entry-header">
						<?php if ( has_post_thumbnail() && ! post_password_required() ) : ?>
						<div class="entry-thumbnail">
							<?php the_post_thumbnail(); ?>
						</div>
						<?php endif; ?>
					</header><!-- .entry-header -->
						<?php the_content(); ?>
			<?php endwhile; ?>
	</div><!-- #primary -->
	<div id="issue-backlist">
		<?php wp_nav_menu( array( 'theme_location' => 'issue-archives-menu' ) ); ?>
	</div>
<?php get_sidebar(); ?>
<?php get_footer(); ?>