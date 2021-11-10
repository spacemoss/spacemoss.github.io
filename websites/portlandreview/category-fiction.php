<?php
/**
 * The main template file.
 *
 * This is the most generic template file in a WordPress theme and one of the
 * two required files for a theme (the other being style.css).
 * It is used to display a page when nothing more specific matches a query.
 * For example, it puts together the home page when no home.php file exists.
 *
 * Learn more: http://codex.wordpress.org/Template_Hierarchy
 *
 * @package WordPress
 * @subpackage Twenty_Thirteen
 * @since Twenty Thirteen 1.0
 */
global $is_front_page_archive;
global $card_body_length;
get_header(); ?>

			<div id="card-area">
				<?php if ( have_posts() ) : ?>
					<div id="column1">
						<?php $my_query = new WP_Query('category_name=fiction&posts_per_page=16');
 			 			$i = 0; while ($my_query->have_posts()) : $my_query->the_post();
  						$do_not_duplicate[] = $post->ID; ?>
							<?php /* The loop */
								if($i == 0) :
							 ?>
			 					<div id="feature-card">
									<?php
									$card_body_length = 590;
									get_template_part( 'content', get_post_format() );
									$i++; ?>
								</div>
					</div>
					<div id="column2">
 							<?php elseif($i < 3) : ?>
 								<div class="right-card">
 									<?php 
 										$card_body_length = 245;
 										get_template_part( 'content', get_post_format() );
 										$i++;
 									?>
 								</div>
 						<?php elseif ($i == 3) : ?>
 							</div>
 							<div id="column3">
 							<?php $i++;
 						elseif ($i <= 6) :
 							if ($i != 6) : ?>
 								<div class="bottom-card">
 									<?php 
 										$card_body_length = 190;
 										get_template_part( 'content', get_post_format() ); 
 										$i++;
 									?>
 								</div>
 							<?php else : ?>
 								<div class="right-card">
 									<?php 
 										$card_body_length = 245;
 										get_template_part( 'content', get_post_format() );
 										$i++;
 									 ?>
 								</div>
 								</div>
 								<div id="bottomrow">
 							<?php endif;
 						elseif ($i <= 9) :
 							if($i != 9) : ?>
 								<div class="bottom-card">
 									<?php 
 										$card_body_length = 190;
 										get_template_part( 'content', get_post_format() );
 										$i++;
 							 		?>
 								</div>
 							<?php else : ?>
								<div class="right-card">
 									<?php 
 										$card_body_length = 245;
 										get_template_part( 'content', get_post_format() ); 
 										$i++;
 									?>
 								</div>
 					</div>
 				</div>
 				<div id="archive-lists">
     				<div id="archives">
 							<?php endif;
 						elseif ($i <= 16) : 
 							$is_front_page_archive = 1;
 						?>
 							<div class="arch-item">
 								<?php get_template_part( 'content', get_post_format() );
 									$is_front_page_archive = 0;
 								 ?>
 							</div>
 						<?php endif;
 					endwhile; ?>
 				</div>
		<?php else :
			get_template_part( 'content', 'none' );
		endif; ?>
			</div> <!-- #card-area -->

<?php get_sidebar(); ?>
<?php get_footer(); ?>